using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Copies Assets/WebGLTemplates/Design1080x720/index.html into every WebGL build output.
/// </summary>
[InitializeOnLoad]
public static class WebGLTemplateEnforcerBootstrap
{
    static WebGLTemplateEnforcerBootstrap()
    {
        Debug.Log(
            "[WebGLTemplate] Editor extension loaded. " +
            "After WebGL build look for SUCCESS in Console, or use Tools > Dice Poker > Inject WebGL Template.");
    }
}

public sealed class WebGLTemplateEnforcer : IPostprocessBuildWithReport, IPostprocessBuildWithContext
{
    public const string VersionMarker = "dice-poker-template-v7";
    const string TemplateRelativePath = "WebGLTemplates/Design1080x720/index.html";

    public int callbackOrder => 999;

    public void OnPostprocessBuild(BuildReport report)
    {
        HandleBuild(report.summary.platform, report.summary.result, report.summary.outputPath, "IPostprocessBuildWithReport");
    }

    public void OnPostprocessBuild(BuildCallbackContext context)
    {
        var summary = context.Report.summary;
        HandleBuild(summary.platform, summary.result, summary.outputPath, "IPostprocessBuildWithContext");
    }

    static void HandleBuild(BuildTarget platform, BuildResult result, string outputPath, string source)
    {
        Debug.Log(
            "[WebGLTemplate] " + source + " fired. Platform=" + platform +
            ", Result=" + result + ", Output=" + outputPath);

        if (platform != BuildTarget.WebGL)
        {
            Debug.LogWarning(
                "[WebGLTemplate] Skipped — platform is " + platform + ", not WebGL. " +
                "(URP log lines alone do not mean this was a WebGL build.)");
            return;
        }

        // Unity 6 often reports Result=Unknown in IPostprocessBuildWithReport (build not finalized yet).
        // IPostprocessBuildWithContext runs after that with the real Succeeded/Failed result.
        if (result == BuildResult.Unknown)
        {
            Debug.Log("[WebGLTemplate] Build still finalizing (Result=Unknown); inject runs from the next callback.");
            return;
        }

        if (result != BuildResult.Succeeded)
        {
            Debug.LogWarning("[WebGLTemplate] WebGL build did not succeed; skipping template inject.");
            return;
        }

        TryInjectIntoFolder(outputPath, logSuccess: true);
    }

    [MenuItem("Tools/Dice Poker/Inject WebGL Template Into Build Folder...")]
    static void InjectFromMenu()
    {
        var folder = EditorUtility.OpenFolderPanel(
            "Select WebGL build folder (must contain index.html)",
            Path.GetDirectoryName(Application.dataPath) ?? "",
            "");

        if (string.IsNullOrEmpty(folder))
            return;

        TryInjectIntoFolder(folder, logSuccess: true);
    }

    [MenuItem("Tools/Dice Poker/Verify Project WebGL Template")]
    static void VerifyProjectTemplate()
    {
        var templatePath = Path.Combine(Application.dataPath, TemplateRelativePath);
        if (!File.Exists(templatePath))
        {
            Debug.LogError("[WebGLTemplate] Missing: " + templatePath);
            return;
        }

        var text = File.ReadAllText(templatePath);
        Debug.Log("[WebGLTemplate] Project template: " + templatePath);
        Debug.Log("[WebGLTemplate] Version v4 present: " + text.Contains(VersionMarker));
        Debug.Log("[WebGLTemplate] Toolbar (#toolbar): " + text.Contains("id=\"toolbar\""));
        Debug.Log("[WebGLTemplate] Fullscreen toggle (#fs-toggle): " + text.Contains("id=\"fs-toggle\""));
    }

    public static bool TryInjectIntoFolder(string outputPath, bool logSuccess)
    {
        var buildDir = outputPath;
        if (string.IsNullOrEmpty(buildDir))
        {
            Debug.LogWarning("[WebGLTemplate] Empty build output path.");
            return false;
        }

        if (File.Exists(buildDir) && buildDir.EndsWith(".html", System.StringComparison.OrdinalIgnoreCase))
            buildDir = Path.GetDirectoryName(buildDir);

        if (string.IsNullOrEmpty(buildDir) || !Directory.Exists(buildDir))
        {
            Debug.LogWarning("[WebGLTemplate] Build folder not found: " + outputPath);
            return false;
        }

        var builtIndexPath = Path.Combine(buildDir, "index.html");
        if (!File.Exists(builtIndexPath))
        {
            Debug.LogWarning(
                "[WebGLTemplate] No index.html in:\n" + buildDir +
                "\nPick the folder that contains index.html and Build/ (your WebGL publish folder).");
            return false;
        }

        var templatePath = Path.Combine(Application.dataPath, TemplateRelativePath);
        if (!File.Exists(templatePath))
        {
            Debug.LogError("[WebGLTemplate] Project template missing: " + templatePath);
            return false;
        }

        var builtHtml = File.ReadAllText(builtIndexPath);
        var templateHtml = File.ReadAllText(templatePath);

        if (!TryExtractBuildTokens(builtHtml, out var tokens))
        {
            Debug.LogError(
                "[WebGLTemplate] Could not parse loader/data URLs from built index.html.\n" +
                "Do a full clean WebGL build (delete old output folder first), then run Inject again.");
            return false;
        }

        var merged = templateHtml
            .Replace("{{{ LOADER_FILENAME }}}", tokens.Loader)
            .Replace("{{{ DATA_FILENAME }}}", tokens.Data)
            .Replace("{{{ FRAMEWORK_FILENAME }}}", tokens.Framework)
            .Replace("{{{ CODE_FILENAME }}}", tokens.Code)
            .Replace("{{{ COMPANY_NAME }}}", tokens.Company)
            .Replace("{{{ PRODUCT_NAME }}}", tokens.Product)
            .Replace("{{{ PRODUCT_VERSION }}}", tokens.Version);

        File.WriteAllText(builtIndexPath, merged);

        if (logSuccess)
        {
            Debug.LogWarning(
                "[WebGLTemplate] *** SUCCESS *** Injected " + VersionMarker + " into:\n" + builtIndexPath +
                "\nRe-upload this entire folder. View-source must contain \"" + VersionMarker + "\".");
        }

        return true;
    }

    static bool TryExtractBuildTokens(string builtHtml, out BuildTokens tokens)
    {
        tokens = new BuildTokens();

        tokens.Loader = Match(builtHtml, @"loaderUrl\s*=\s*buildUrl\s*\+\s*""/([^""]+)""");
        tokens.Data = Match(builtHtml, @"dataUrl:\s*buildUrl\s*\+\s*""/([^""]+)""");
        tokens.Framework = Match(builtHtml, @"frameworkUrl:\s*buildUrl\s*\+\s*""/([^""]+)""");
        tokens.Code = Match(builtHtml, @"codeUrl:\s*buildUrl\s*\+\s*""/([^""]+)""");
        tokens.Company = Match(builtHtml, @"companyName:\s*""([^""]*)""");
        tokens.Product = Match(builtHtml, @"productName:\s*""([^""]*)""");
        tokens.Version = Match(builtHtml, @"productVersion:\s*""([^""]*)""");

        return !string.IsNullOrEmpty(tokens.Loader)
               && !string.IsNullOrEmpty(tokens.Data)
               && !string.IsNullOrEmpty(tokens.Framework)
               && !string.IsNullOrEmpty(tokens.Code);
    }

    static string Match(string text, string pattern)
    {
        var m = Regex.Match(text, pattern);
        return m.Success ? m.Groups[1].Value : string.Empty;
    }

    struct BuildTokens
    {
        public string Loader;
        public string Data;
        public string Framework;
        public string Code;
        public string Company;
        public string Product;
        public string Version;
    }
}
