using System.Collections.Generic;
using System.Threading.Tasks;
using WpfDesktop.Models;

namespace WpfDesktop.Services.Interfaces;

public interface IGitService
{
    Task<bool> IsGitRepositoryAsync(string path);
    Task<string> GetRemoteUrlAsync(string path);
    Task<string> GetCurrentBranchAsync(string path);
    Task<string> GetCurrentCommitHashAsync(string path);
    Task<IReadOnlyList<GitCommit>> GetCommitsAsync(string path, int count = 100);
    Task<IReadOnlyList<GitCommit>> GetTagsAsync(string path);
    Task CheckoutAsync(string path, string refName);
    Task FetchAsync(string path);
}
