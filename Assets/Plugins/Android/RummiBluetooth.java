package com.domezos.rommecup;

import android.annotation.SuppressLint;
import android.app.Activity;
import android.bluetooth.BluetoothAdapter;
import android.bluetooth.BluetoothDevice;
import android.bluetooth.BluetoothManager;
import android.bluetooth.BluetoothServerSocket;
import android.bluetooth.BluetoothSocket;
import android.content.BroadcastReceiver;
import android.content.Context;
import android.content.Intent;
import android.content.IntentFilter;
import android.util.Log;
import java.io.BufferedReader;
import java.io.InputStreamReader;
import java.io.OutputStream;
import java.nio.charset.StandardCharsets;
import java.util.Map;
import java.util.UUID;
import java.util.concurrent.ConcurrentHashMap;
import java.util.concurrent.ConcurrentLinkedQueue;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;

@SuppressLint("MissingPermission")
public class RummiBluetooth {
    static final String TAG = "RC.Bt";
    static final UUID ID = UUID.fromString("7d3c1a52-9b8e-4f3a-a6c1-52e0c0ffee42");
    final Activity act;
    final BluetoothAdapter ad;
    final ConcurrentLinkedQueue<String> q = new ConcurrentLinkedQueue<>();
    final Map<Integer, Conn> conns = new ConcurrentHashMap<>();
    int nextId = 1, maxClients = 3;
    volatile boolean hosting;
    BluetoothServerSocket server;
    BroadcastReceiver rcv;

    public RummiBluetooth(Activity a) {
        act = a;
        BluetoothManager m = (BluetoothManager) a.getSystemService(Context.BLUETOOTH_SERVICE);
        ad = m != null ? m.getAdapter() : null;
        Log.i(TAG, "init adapter=" + (ad != null));
    }

    void ev(String s) { q.add(s); Log.i(TAG, s.length() > 120 ? s.substring(0, 120) : s); }

    void err(String where, Exception e) { ev("E|" + where + ": " + e.getMessage()); }

    public boolean supported() { return ad != null; }

    public boolean enabled() { return ad != null && ad.isEnabled(); }

    public String poll() { return q.poll(); }

    public String localName() {
        try { String n = ad.getName(); return n != null ? n : "Android"; } catch (Exception e) { return "Android"; }
    }

    public void requestEnable() {
        act.runOnUiThread(() -> { try { act.startActivity(new Intent(BluetoothAdapter.ACTION_REQUEST_ENABLE)); } catch (Exception e) { err("enable", e); } });
    }

    public void makeDiscoverable(int sec) {
        act.runOnUiThread(() -> {
            try {
                Intent i = new Intent(BluetoothAdapter.ACTION_REQUEST_DISCOVERABLE);
                i.putExtra(BluetoothAdapter.EXTRA_DISCOVERABLE_DURATION, sec);
                act.startActivity(i);
            } catch (Exception e) { err("discoverable", e); }
        });
    }

    public void startHost(int max) {
        maxClients = max;
        hosting = true;
        new Thread(() -> {
            try {
                server = ad.listenUsingInsecureRfcommWithServiceRecord("RommeCupVariety", ID);
                ev("H|listening");
                while (hosting) {
                    BluetoothSocket s = server.accept();
                    if (s == null) continue;
                    if (conns.size() >= maxClients) { s.close(); continue; }
                    add(s);
                }
            } catch (Exception e) { if (hosting) err("host", e); }
        }, "RC-BtAccept").start();
    }

    public void stopAccepting() {
        hosting = false;
        try { if (server != null) server.close(); } catch (Exception ignored) { }
    }

    public void startDiscovery() {
        try {
            if (rcv == null) {
                rcv = new BroadcastReceiver() {
                    @Override public void onReceive(Context c, Intent i) {
                        if (BluetoothDevice.ACTION_FOUND.equals(i.getAction())) {
                            BluetoothDevice d = i.getParcelableExtra(BluetoothDevice.EXTRA_DEVICE);
                            if (d != null) ev("F|" + d.getAddress() + "|" + (d.getName() != null ? d.getName() : d.getAddress()));
                        } else if (BluetoothAdapter.ACTION_DISCOVERY_FINISHED.equals(i.getAction())) ev("Z|done");
                    }
                };
                IntentFilter f = new IntentFilter(BluetoothDevice.ACTION_FOUND);
                f.addAction(BluetoothAdapter.ACTION_DISCOVERY_FINISHED);
                act.registerReceiver(rcv, f);
            }
            for (BluetoothDevice d : ad.getBondedDevices()) ev("F|" + d.getAddress() + "|" + d.getName() + " (gekoppelt)");
            if (ad.isDiscovering()) ad.cancelDiscovery();
            if (!ad.startDiscovery()) ev("E|Suche konnte nicht starten (Standort/Berechtigung?)");
        } catch (Exception e) { err("discovery", e); }
    }

    public void stopDiscovery() {
        try { if (ad.isDiscovering()) ad.cancelDiscovery(); } catch (Exception ignored) { }
    }

    public void connect(String addr) {
        new Thread(() -> {
            try {
                stopDiscovery();
                BluetoothDevice d = ad.getRemoteDevice(addr);
                BluetoothSocket s = d.createInsecureRfcommSocketToServiceRecord(ID);
                s.connect();
                add(s);
            } catch (Exception e) { ev("X|" + addr + "|" + e.getMessage()); }
        }, "RC-BtConnect").start();
    }

    void add(BluetoothSocket s) throws Exception {
        int id;
        synchronized (this) { id = nextId++; }
        Conn c = new Conn(id, s);
        conns.put(id, c);
        String n;
        try { n = s.getRemoteDevice().getName(); } catch (Exception e) { n = "?"; }
        ev("C|" + id + "|" + n);
        c.start();
    }

    public boolean send(int id, String line) {
        Conn c = conns.get(id);
        if (c == null) return false;
        c.write(line);
        return true;
    }

    public void close(int id) {
        Conn c = conns.remove(id);
        if (c == null) return;
        try { c.out.execute(c::shut); } catch (Exception e) { c.shut(); }
    }

    public void shutdown() {
        stopAccepting();
        stopDiscovery();
        for (Conn c : conns.values()) c.shut();
        conns.clear();
        try { if (rcv != null) act.unregisterReceiver(rcv); } catch (Exception ignored) { }
        rcv = null;
        q.clear();
    }

    class Conn extends Thread {
        final int id;
        final BluetoothSocket s;
        final ExecutorService out = Executors.newSingleThreadExecutor();
        OutputStream os;
        volatile boolean alive = true;

        Conn(int id, BluetoothSocket s) { super("RC-BtConn" + id); this.id = id; this.s = s; }

        public void run() {
            try {
                os = s.getOutputStream();
                BufferedReader in = new BufferedReader(new InputStreamReader(s.getInputStream(), StandardCharsets.UTF_8));
                String line;
                while (alive && (line = in.readLine()) != null) q.add("M|" + id + "|" + line);
            } catch (Exception e) { Log.w(TAG, "conn " + id + " " + e.getMessage()); }
            conns.remove(id);
            if (alive) ev("D|" + id);
            shut();
        }

        void write(String line) {
            out.execute(() -> {
                try {
                    OutputStream o = os != null ? os : s.getOutputStream();
                    o.write((line + "\n").getBytes(StandardCharsets.UTF_8));
                    o.flush();
                } catch (Exception e) { Log.w(TAG, "write " + id + " " + e.getMessage()); }
            });
        }

        void shut() {
            alive = false;
            try { s.close(); } catch (Exception ignored) { }
            out.shutdown();
        }
    }
}
