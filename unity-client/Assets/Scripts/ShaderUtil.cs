using UnityEngine;

/// <summary>
/// Finds a shader that will actually render, regardless of which render
/// pipeline the project is on -- instead of hardcoding one pipeline's
/// shader name and hoping.
///
/// Real bug this fixes: RuntimeCityBuilder and MonsterAvatar originally
/// called Shader.Find("Standard") directly -- the Built-in Render
/// Pipeline's shader -- in a project created with the URP template
/// (docs/12 decision log: Unity 6000.3.13f1, URP). URP either doesn't
/// ship that shader or can't render it, and Unity's fallback for "shader
/// incompatible with the active pipeline" is to render the material
/// bright magenta with no Console error at all -- confirmed against a
/// real Editor run: "pink buildings no errors."
/// </summary>
public static class ShaderUtil
{
    public static Shader FindRenderableShader()
    {
        var candidates = new[]
        {
            "Universal Render Pipeline/Lit", // URP -- this project's actual pipeline
            "Standard",                       // Built-in Render Pipeline
            "Unlit/Color",                    // last resort: exists in nearly every configuration
        };
        foreach (var name in candidates)
        {
            var shader = Shader.Find(name);
            if (shader != null) return shader;
        }
        return null; // caller decides how to handle a truly shaderless environment
    }
}
