using UnityEngine;

namespace RommeCup.Core
{
    public static class Fx
    {
        static Material mote;

        public static void Init()
        {
            var dot = new Pix(32, 32, new Color(1, 1, 1, 0));
            dot.Fill((x, y) => { float d = Mathf.Sqrt((x - 16) * (x - 16) + (y - 16) * (y - 16)) / 16f; return new Color(1, 1, 1, Mathf.Clamp01(1 - d) * Mathf.Clamp01(1 - d)); });
            mote = Mats.Particle(dot.Tex(false), true);
        }

        public static ParticleSystem Dust(Transform parent, Vector3 pos, Vector3 area)
        {
            var go = new GameObject("Dust");
            go.transform.SetParent(parent); go.transform.position = pos;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            go.GetComponent<ParticleSystemRenderer>().material = mote;
            var m = ps.main;
            m.loop = true; m.playOnAwake = false; m.simulationSpace = ParticleSystemSimulationSpace.World; m.duration = 10; m.startLifetime = 14; m.startSpeed = 0;
            m.startSize = new ParticleSystem.MinMaxCurve(.03f, .07f); m.maxParticles = 70; m.prewarm = true;
            m.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, .92f, .8f, .5f), new Color(1f, .85f, .7f, .25f));
            var e = ps.emission; e.rateOverTime = 5;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = area;
            var n = ps.noise; n.enabled = true; n.strength = .12f; n.frequency = .15f; n.scrollSpeed = .05f;
            var v = ps.velocityOverLifetime; v.enabled = true; v.space = ParticleSystemSimulationSpace.World; v.x = new ParticleSystem.MinMaxCurve(-.02f, .02f); v.y = new ParticleSystem.MinMaxCurve(-.03f, .02f); v.z = new ParticleSystem.MinMaxCurve(-.02f, .02f);
            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) }, new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, .3f), new GradientAlphaKey(1, .7f), new GradientAlphaKey(0, 1) });
            col.color = g;
            ps.Play();
            return ps;
        }
    }
}
