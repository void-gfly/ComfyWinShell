using System.Text;
using WpfDesktop.Services;
using Xunit;

namespace WpfDesktop.Tests.Services;

public sealed class ExtraModelPathsYamlHelperTests : IDisposable
{
    private readonly string _modelsRoot = Path.Combine(
        Path.GetTempPath(), "WpfDesktopTests", Guid.NewGuid().ToString("N"));

    public ExtraModelPathsYamlHelperTests()
    {
        Directory.CreateDirectory(_modelsRoot);
    }

    [Theory]
    [InlineData("text_encoders")]
    [InlineData("hunyuan_video")]
    [InlineData("任意新分类")]
    public void GenerateYamlContent_WithOnlyUncommonCategory_MapsDirectoryDirectly(string category)
    {
        Directory.CreateDirectory(Path.Combine(_modelsRoot, category));

        var yaml = ExtraModelPathsYamlHelper.GenerateYamlContent(_modelsRoot);

        Assert.Contains($"    base_path: {_modelsRoot.Replace('\\', '/')}/", yaml);
        Assert.Contains($"    {category}: {category}/", yaml);
        Assert.DoesNotContain($"models/{category}/", yaml);
    }

    [Fact]
    public void GenerateYamlContent_WithCommonAndCustomEmptyCategories_IncludesAllInSortedOrder()
    {
        foreach (var category in new[] { "ZCustom", "loras", "checkpoints", "AnotherCustom" })
        {
            Directory.CreateDirectory(Path.Combine(_modelsRoot, category));
        }

        var yaml = ExtraModelPathsYamlHelper.GenerateYamlContent(_modelsRoot);

        Assert.EndsWith(string.Join(Environment.NewLine,
            "    AnotherCustom: AnotherCustom/",
            "    checkpoints: checkpoints/",
            "    loras: loras/",
            "    ZCustom: ZCustom/",
            string.Empty), yaml);
    }

    [Fact]
    public void GenerateYamlContent_WithNestedModelsDirectoryAndFile_MapsOnlyImmediateDirectories()
    {
        Directory.CreateDirectory(Path.Combine(_modelsRoot, "models", "nested_category"));
        Directory.CreateDirectory(Path.Combine(_modelsRoot, "loras", "nested_loras"));
        File.WriteAllText(Path.Combine(_modelsRoot, "notes.txt"), "not a model category", Encoding.UTF8);

        var yaml = ExtraModelPathsYamlHelper.GenerateYamlContent(_modelsRoot);

        Assert.EndsWith(string.Join(Environment.NewLine,
            "    loras: loras/",
            "    models: models/",
            string.Empty), yaml);
        Assert.DoesNotContain("nested_category", yaml);
        Assert.DoesNotContain("nested_loras", yaml);
        Assert.DoesNotContain("notes.txt", yaml);
    }

    [Fact]
    public void GenerateYamlContent_WithTrailingSeparatorAndSpaces_NormalizesBasePath()
    {
        var root = Path.Combine(_modelsRoot, "模型 库");
        Directory.CreateDirectory(Path.Combine(root, "loras"));

        var yaml = ExtraModelPathsYamlHelper.GenerateYamlContent(root + Path.DirectorySeparatorChar);

        Assert.Contains($"    base_path: {root.Replace('\\', '/')}/" + Environment.NewLine, yaml);
        Assert.Contains("    loras: loras/", yaml);
        Assert.DoesNotContain('\\', yaml);
    }

    [Fact]
    public void GenerateYamlContent_WithEmptyRoot_HasNoCategoryMappings()
    {
        var yaml = ExtraModelPathsYamlHelper.GenerateYamlContent(_modelsRoot);

        Assert.EndsWith($"    base_path: {_modelsRoot.Replace('\\', '/')}/" + Environment.NewLine, yaml);
    }

    [Fact]
    public void GenerateYamlContent_WithMissingRoot_ThrowsInsteadOfGeneratingPartialConfiguration()
    {
        Assert.Throws<DirectoryNotFoundException>(() =>
            ExtraModelPathsYamlHelper.GenerateYamlContent(Path.Combine(_modelsRoot, "missing")));
    }

    public void Dispose()
    {
        Directory.Delete(_modelsRoot, recursive: true);
    }
}
