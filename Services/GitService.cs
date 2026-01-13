using System.Diagnostics;
using System.IO;
using System.Text;
using System.ComponentModel;
using WpfDesktop.Models;
using WpfDesktop.Services.Interfaces;

namespace WpfDesktop.Services;

public class GitService : IGitService
{
    public async Task<bool> IsGitRepositoryAsync(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
            return false;

        try
        {
            var result = await RunGitCommandAsync(path, "rev-parse --is-inside-work-tree");
            return result.Trim() == "true";
        }
        catch
        {
            return false;
        }
    }

    public async Task<string> GetRemoteUrlAsync(string path)
    {
        try
        {
            return (await RunGitCommandAsync(path, "remote get-url origin")).Trim();
        }
        catch
        {
            return string.Empty;
        }
    }

    public async Task<string> GetCurrentBranchAsync(string path)
    {
        try
        {
            // First try to get branch name
            var branch = (await RunGitCommandAsync(path, "branch --show-current")).Trim();
            if (!string.IsNullOrEmpty(branch))
                return branch;

            // If detached HEAD (e.g. checked out a tag/commit), show the hash or tag
            return "Detached HEAD";
        }
        catch
        {
            return "Unknown";
        }
    }

    public async Task<string> GetCurrentCommitHashAsync(string path)
    {
        try
        {
            return (await RunGitCommandAsync(path, "rev-parse HEAD")).Trim();
        }
        catch
        {
            return string.Empty;
        }
    }

    public async Task<IReadOnlyList<GitCommit>> GetCommitsAsync(string path, int count = 100)
    {
        var commits = new List<GitCommit>();
        try
        {
            // Format: Hash|ShortHash|Message|Date|Author|Refs
            // Use --no-pager to ensure we get raw output
            var output = await RunGitCommandAsync(path, $"--no-pager log --pretty=format:\"%H|%h|%s|%ai|%an|%D\" -n {count}");
            var currentHash = await GetCurrentCommitHashAsync(path);

            using var reader = new StringReader(output);
            string? line;
            while ((line = await reader.ReadLineAsync()) != null)
            {
                var parts = line.Split('|');
                if (parts.Length < 5) continue;

                var commit = new GitCommit
                {
                    Hash = parts[0],
                    ShortHash = parts[1],
                    Message = parts[2],
                    Date = DateTime.TryParse(parts[3], out var date) ? date : DateTime.MinValue,
                    Author = parts[4],
                    IsCurrent = parts[0] == currentHash,
                    Tag = parts.Length > 5 ? ParseTag(parts[5]) : null
                };
                commits.Add(commit);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error getting commits: {ex.Message}");
            throw; // Re-throw to let ViewModel handle it
        }
        return commits;
    }

    public async Task<IReadOnlyList<GitCommit>> GetTagsAsync(string path)
    {
        var tags = new List<GitCommit>();
        try
        {
            // Get tags sorted by date desc
            // We use git log --tags --simplify-by-decoration to get commits that have tags
            var output = await RunGitCommandAsync(path, "--no-pager log --tags --simplify-by-decoration --pretty=format:\"%H|%h|%s|%ai|%an|%D\"");
            var currentHash = await GetCurrentCommitHashAsync(path);

            using var reader = new StringReader(output);
            string? line;
            while ((line = await reader.ReadLineAsync()) != null)
            {
                var parts = line.Split('|');
                if (parts.Length < 6) continue; // Must have refs for tags

                var refString = parts[5];
                var tagName = ParseTag(refString);

                if (string.IsNullOrEmpty(tagName)) continue;

                var commit = new GitCommit
                {
                    Hash = parts[0],
                    ShortHash = parts[1],
                    Message = parts[2],
                    Date = DateTime.TryParse(parts[3], out var date) ? date : DateTime.MinValue,
                    Author = parts[4],
                    IsCurrent = parts[0] == currentHash,
                    Tag = tagName
                };
                tags.Add(commit);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error getting tags: {ex.Message}");
            throw;
        }
        return tags;
    }

    public async Task CheckoutAsync(string path, string refName)
    {
        await RunGitCommandAsync(path, $"checkout {refName}");
    }

    public async Task FetchAsync(string path)
    {
        await RunGitCommandAsync(path, "fetch --all --tags");
    }

    private string? ParseTag(string refString)
    {
        // Example refs: "HEAD -> master, tag: v1.0, origin/master"
        // We look for "tag: xxx"
        var parts = refString.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < parts.Length; i++)
        {
            if (parts[i] == "tag:" && i + 1 < parts.Length)
            {
                return parts[i + 1];
            }
        }
        return null;
    }

    private async Task<string> RunGitCommandAsync(string workingDir, string arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = arguments,
            WorkingDirectory = workingDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8
        };

        using var process = new Process { StartInfo = startInfo };
        var outputBuilder = new StringBuilder();
        var errorBuilder = new StringBuilder();

        process.OutputDataReceived += (_, e) => { if (e.Data != null) outputBuilder.AppendLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data != null) errorBuilder.AppendLine(e.Data); };

        try
        {
            process.Start();
        }
        catch (Win32Exception ex)
        {
            if (ex.NativeErrorCode == 2) // File not found
            {
                throw new FileNotFoundException("Git executable not found. Please install Git and ensure it is in your PATH.", "git");
            }
            throw;
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            var error = errorBuilder.ToString();
            // Git often outputs progress to stderr, so only treat as error if exit code is non-zero
            throw new Exception($"Git command failed (ExitCode {process.ExitCode}): {error}");
        }

        return outputBuilder.ToString();
    }
}
