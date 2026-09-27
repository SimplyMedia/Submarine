using Microsoft.EntityFrameworkCore;
using Submarine.Core.Entities;
using Submarine.Core.Enums;
using Submarine.Core.Notifications;
using Submarine.Infrastructure.Persistence;

namespace Submarine.Infrastructure.Notifications;

public sealed class NotificationConfigurationService(SubmarineDbContext db)
{
	public async Task<Notification> SaveAsync(int? id, NotificationConfiguration request, CancellationToken cancellationToken = default)
	{
		if (!NotificationSettingsJson.Validate(request.Type, request.SettingsJson).IsValid)
			throw new InvalidOperationException("Notification settings are invalid");

		Notification notification;
		if (id is { } notificationId)
		{
			notification = await db.Notifications.Include(x => x.Tags).FirstOrDefaultAsync(x => x.Id == notificationId, cancellationToken)
				?? throw new KeyNotFoundException($"Notification {notificationId} does not exist");
		}
		else
		{
			notification = new Notification();
			db.Notifications.Add(notification);
		}

		notification.Name = request.Name;
		notification.Type = request.Type;
		notification.Enable = request.Enable;
		notification.SettingsJson = request.SettingsJson;
		notification.OnGrab = request.OnGrab;
		notification.OnImport = request.OnImport;
		notification.OnUpgrade = request.OnUpgrade;
		notification.OnRename = request.OnRename;
		notification.OnDelete = request.OnDelete;
		notification.OnHealthIssue = request.OnHealthIssue;
		notification.OnHealthRestored = request.OnHealthRestored;
		notification.OnApplicationUpdate = request.OnApplicationUpdate;
		notification.OnManualInteractionRequired = request.OnManualInteractionRequired;
		notification.IncludeHealthWarnings = request.IncludeHealthWarnings;

		var labels = request.Tags.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
		var tags = await db.Tags.Where(tag => labels.Contains(tag.Label)).ToListAsync(cancellationToken);
		foreach (var label in labels.Except(tags.Select(x => x.Label), StringComparer.OrdinalIgnoreCase))
		{
			var tag = new Tag { Label = label };
			db.Tags.Add(tag);
			tags.Add(tag);
		}
		notification.Tags = tags;
		await db.SaveChangesAsync(cancellationToken);
		await db.Entry(notification).Collection(x => x.Tags).LoadAsync(cancellationToken);
		return notification;
	}

	public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
	{
		var notification = await db.Notifications.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
			?? throw new KeyNotFoundException($"Notification {id} does not exist");
		db.Notifications.Remove(notification);
		await db.SaveChangesAsync(cancellationToken);
	}
}

public sealed record NotificationConfiguration(
	string Name,
	NotificationType Type,
	bool Enable,
	string SettingsJson,
	bool OnGrab,
	bool OnImport,
	bool OnUpgrade,
	bool OnRename,
	bool OnDelete,
	bool OnHealthIssue,
	bool OnHealthRestored,
	bool OnApplicationUpdate,
	bool OnManualInteractionRequired,
	bool IncludeHealthWarnings,
	IReadOnlyList<string> Tags);
