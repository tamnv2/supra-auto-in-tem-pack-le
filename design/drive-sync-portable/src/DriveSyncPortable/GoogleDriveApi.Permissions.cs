using System.Text.Json;

namespace DriveSyncPortable;

internal sealed record DrivePermission(string Id, string Email, string Role, string Type);

internal sealed partial class GoogleDriveApi
{
    internal async Task<List<DrivePermission>> ReadPermissionsInScopeAsync(
        string fileId, string rootId, CancellationToken ct)
    {
        await AssertFileInSelectedRootAsync(fileId, rootId, ct);
        string url = "https://www.googleapis.com/drive/v3/files/" + Param(fileId)
            + "/permissions?fields="
            + Param("nextPageToken,permissions(id,type,role,emailAddress)")
            + "&pageSize=100";
        using var json = await JsonAsync(HttpMethod.Get, url, null, ct);
        return json.RootElement.GetProperty("permissions").EnumerateArray()
            .Select(x => new DrivePermission(GetString(x, "id"),
                GetString(x, "emailAddress"), GetString(x, "role"),
                GetString(x, "type"))).ToList();
    }

    internal async Task AddPermissionInScopeAsync(string fileId, string rootId,
        string email, string role, CancellationToken ct)
    {
        await AssertFileInSelectedRootAsync(fileId, rootId, ct);
        if (!System.Net.Mail.MailAddress.TryCreate(email, out var address)
            || !address.Address.Equals(email.Trim(), StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Email không hợp lệ.");
        if (role is not ("reader" or "writer"))
            throw new ArgumentException("Chỉ hỗ trợ quyền Viewer hoặc Editor.");
        string url = "https://www.googleapis.com/drive/v3/files/" + Param(fileId)
            + "/permissions?fields=id,role,emailAddress&sendNotificationEmail=false";
        using var _ = await JsonAsync(HttpMethod.Post, url,
            new { type = "user", role, emailAddress = email.Trim() }, ct);
    }

    internal async Task RemovePermissionInScopeAsync(string fileId, string rootId,
        string permissionId, CancellationToken ct)
    {
        await AssertFileInSelectedRootAsync(fileId, rootId, ct);
        if (string.IsNullOrWhiteSpace(permissionId))
            throw new ArgumentException("Thiếu ID quyền chia sẻ.");
        string url = "https://www.googleapis.com/drive/v3/files/" + Param(fileId)
            + "/permissions/" + Param(permissionId);
        using var response = await SendAsync(HttpMethod.Delete, url, null, ct);
    }
}
