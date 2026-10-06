using UnityEngine;

namespace RommeCup.Core
{
    public static class Mats
    {
        static Material lit, emit, litN, emitN, add, alpha;

        public static void Init()
        {
            lit = Resources.Load<Material>("RC_Lit"); if (!lit) lit = new Material(Shader.Find("Standard"));
            emit = Resources.Load<Material>("RC_LitEmit"); if (!emit) emit = lit;
            litN = Resources.Load<Material>("RC_LitN"); if (!litN) { litN = new Material(lit); litN.EnableKeyword("_NORMALMAP"); }
            emitN = Resources.Load<Material>("RC_LitNE"); if (!emitN) { emitN = new Material(emit); emitN.EnableKeyword("_NORMALMAP"); }
            add = Resources.Load<Material>("RC_Add"); if (!add) add = new Material(Shader.Find("Legacy Shaders/Particles/Additive"));
            alpha = Resources.Load<Material>("RC_Alpha"); if (!alpha) alpha = new Material(Shader.Find("Legacy Shaders/Particles/Alpha Blended"));
            Log.I("Mats", "lit=" + lit.shader.name + " litN=" + litN.IsKeywordEnabled("_NORMALMAP") + " emitN=" + emitN.IsKeywordEnabled("_EMISSION"));
        }

        static Material Make(Material src, Color c, float metal, float gloss, Texture tex, Texture normal, float bump)
        {
            var m = new Material(src) { color = c };
            m.SetFloat("_Metallic", metal); m.SetFloat("_Glossiness", gloss);
            if (tex) m.mainTexture = tex;
            if (normal) { m.SetTexture("_BumpMap", normal); m.SetFloat("_BumpScale", bump); }
            return m;
        }

        public static Material Lit(Color c, float metal = 0, float gloss = .5f, Texture tex = null, Texture normal = null, float bump = 1) => Make(normal ? litN : lit, c, metal, gloss, tex, normal, bump);

        public static Material Emissive(Color c, float metal = 0, float gloss = .5f, Texture tex = null, Texture normal = null, float bump = 1)
        {
            var m = Make(normal ? emitN : emit, c, metal, gloss, tex, normal, bump);
            m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", Color.black);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            return m;
        }

        public static Material Particle(Texture tex, bool additive) => new Material(additive ? add : alpha) { mainTexture = tex };
    }
}
