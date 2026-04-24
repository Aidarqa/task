using Microsoft.EntityFrameworkCore;
using TrelloClone.Api.Data;
using TrelloClone.Shared.Models;

namespace TrelloClone.Api.Services;

public class DeadlineReminderService(
    IServiceScopeFactory scopeFactory,
    ILogger<DeadlineReminderService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await Task.WhenAll(
            RunDeadlineLoopAsync(ct),
            RunEventReminderLoopAsync(ct));
    }

    private async Task RunDeadlineLoopAsync(CancellationToken ct)
    {
        await Task.Delay(TimeSpan.FromMinutes(2), ct);
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await CheckDeadlinesAsync(ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Deadline check failed");
            }

            await Task.Delay(TimeSpan.FromHours(6), ct);
        }
    }

    private async Task RunEventReminderLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await CheckUpcomingEventsAsync(ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Event reminder check failed");
            }

            await Task.Delay(TimeSpan.FromMinutes(5), ct);
        }
    }

    private async Task CheckDeadlinesAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var notif = scope.ServiceProvider.GetRequiredService<INotificationService>();

        var today = DateTime.UtcNow.Date;
        var tomorrow = today.AddDays(1);

        var dueTomorrow = await db.WorkTasks
            .Where(t => t.DueDate.HasValue
                && t.DueDate.Value.Date == tomorrow
                && t.Status != WorkTaskStatus.Done
                && t.Status != WorkTaskStatus.Cancelled
                && t.AssigneeId != null)
            .ToListAsync(ct);

        foreach (var task in dueTomorrow)
        {
            var alreadySent = await db.Notifications.AnyAsync(
                n => n.RelatedEntityId == task.Id.ToString()
                  && n.Title == "Срок задачи"
                  && n.CreatedAt.Date == today,
                ct);

            if (!alreadySent)
            {
                await notif.SendAsync(
                    task.AssigneeId!,
                    "Срок задачи",
                    $"Срок выполнения задачи \"{task.Title}\" истекает завтра.",
                    NotificationType.Task,
                    $"/tasks/{task.Id}",
                    task.Id.ToString());
            }
        }

        var overdue = await db.WorkTasks
            .Where(t => t.DueDate.HasValue
                && t.DueDate.Value.Date < today
                && t.Status != WorkTaskStatus.Done
                && t.Status != WorkTaskStatus.Cancelled
                && t.AssigneeId != null)
            .ToListAsync(ct);

        foreach (var task in overdue)
        {
            var alreadySent = await db.Notifications.AnyAsync(
                n => n.RelatedEntityId == task.Id.ToString()
                  && n.Title == "Задача просрочена"
                  && n.CreatedAt.Date == today,
                ct);

            if (!alreadySent)
            {
                await notif.SendAsync(
                    task.AssigneeId!,
                    "Задача просрочена",
                    $"Задача \"{task.Title}\" просрочена.",
                    NotificationType.Task,
                    $"/tasks/{task.Id}",
                    task.Id.ToString());
            }
        }

        var projectsEndingTomorrow = await db.Projects
            .Where(p => p.EndDate.HasValue
                && p.EndDate.Value.Date == tomorrow
                && p.Status == ProjectStatus.Active)
            .ToListAsync(ct);

        foreach (var project in projectsEndingTomorrow)
        {
            var alreadySent = await db.Notifications.AnyAsync(
                n => n.RelatedEntityId == project.Id.ToString()
                  && n.Title == "Срок проекта"
                  && n.CreatedAt.Date == today,
                ct);

            if (!alreadySent)
            {
                await notif.SendAsync(
                    project.OwnerId,
                    "Срок проекта",
                    $"По проекту \"{project.Name}\" завтра истекает срок.",
                    NotificationType.System,
                    "/projects",
                    project.Id.ToString());
            }
        }
    }

    private async Task CheckUpcomingEventsAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var notif = scope.ServiceProvider.GetRequiredService<INotificationService>();

        var now = KyrgyzstanTime.Now;
        await SendEventReminderAsync(db, notif, now, 60, 55, 65, ct);
        await SendEventReminderAsync(db, notif, now, 30, 25, 35, ct);
    }

    private static async Task SendEventReminderAsync(
        AppDbContext db,
        INotificationService notif,
        DateTime now,
        int reminderMinutes,
        int windowStartMinutes,
        int windowEndMinutes,
        CancellationToken ct)
    {
        var windowStart = now.AddMinutes(windowStartMinutes);
        var windowEnd = now.AddMinutes(windowEndMinutes);

        var upcomingEvents = await db.CalendarEvents
            .Include(e => e.Participants)
            .Where(e => e.StartTime >= windowStart && e.StartTime <= windowEnd)
            .ToListAsync(ct);

        foreach (var ev in upcomingEvents)
        {
            var recipients = ev.Participants.Select(p => p.UserId)
                .Append(ev.OrganizerId)
                .Distinct()
                .ToList();

            foreach (var userId in recipients)
            {
                var body = reminderMinutes == 60
                    ? $"Через 1 час: \"{ev.Title}\" в {ev.StartTime:dd.MM.yyyy HH:mm}"
                    : $"Через 30 минут: \"{ev.Title}\" в {ev.StartTime:dd.MM.yyyy HH:mm}";

                var alreadySent = await db.Notifications.AnyAsync(
                    n => n.RelatedEntityId == ev.Id.ToString()
                      && n.UserId == userId
                      && n.Title == "Напоминание о событии"
                      && n.Body == body,
                    ct);

                if (!alreadySent)
                {
                    await notif.SendAsync(
                        userId,
                        "Напоминание о событии",
                        body,
                        NotificationType.Event,
                        "/calendar",
                        ev.Id.ToString());
                }
            }
        }
    }
}
