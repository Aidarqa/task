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
        try
        {
            await Task.Delay(TimeSpan.FromMinutes(2), ct);
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    await CheckDeadlinesAsync(ct);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Deadline check failed");
                }

                await Task.Delay(TimeSpan.FromHours(6), ct);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { /* shutdown */ }
    }

    private async Task RunEventReminderLoopAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    await CheckUpcomingEventsAsync(ct);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Event reminder check failed");
                }

                await Task.Delay(TimeSpan.FromMinutes(5), ct);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { /* shutdown */ }
    }

    private async Task CheckDeadlinesAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var notif = scope.ServiceProvider.GetRequiredService<INotificationService>();

        var todayUtcStart = KyrgyzstanTime.TodayUtcStart;
        var tomorrowUtcStart = KyrgyzstanTime.TomorrowUtcStart;
        var dayAfterTomorrowUtcStart = tomorrowUtcStart.AddDays(1);

        var dueTomorrow = await db.WorkTasks
            .Include(t => t.Assignees)
            .Where(t => t.DueDate.HasValue
                && t.DueDate.Value >= tomorrowUtcStart
                && t.DueDate.Value < dayAfterTomorrowUtcStart
                && t.Status != WorkTaskStatus.Done
                && t.Status != WorkTaskStatus.Cancelled
                && t.Assignees.Any())
            .ToListAsync(ct);

        foreach (var task in dueTomorrow)
        {
            var alreadySent = await db.Notifications.AnyAsync(
                n => n.RelatedEntityId == task.Id.ToString()
                  && n.Title == "Срок задачи"
                  && n.CreatedAt >= todayUtcStart
                  && n.CreatedAt < tomorrowUtcStart,
                ct);

            if (!alreadySent)
            {
                await notif.SendToManyAsync(
                    task.Assignees.Select(a => a.UserId),
                    "Срок задачи",
                    $"Срок выполнения задачи «{task.Title}» истекает завтра.",
                    NotificationType.Task,
                    $"/tasks/{task.Id}",
                    task.Id.ToString());
            }
        }

        var overdue = await db.WorkTasks
            .Include(t => t.Assignees)
            .Where(t => t.DueDate.HasValue
                && t.DueDate.Value < todayUtcStart
                && t.Status != WorkTaskStatus.Done
                && t.Status != WorkTaskStatus.Cancelled
                && t.Assignees.Any())
            .ToListAsync(ct);

        foreach (var task in overdue)
        {
            var alreadySent = await db.Notifications.AnyAsync(
                n => n.RelatedEntityId == task.Id.ToString()
                  && n.Title == "Задача просрочена"
                  && n.CreatedAt >= todayUtcStart
                  && n.CreatedAt < tomorrowUtcStart,
                ct);

            if (!alreadySent)
            {
                await notif.SendToManyAsync(
                    task.Assignees.Select(a => a.UserId),
                    "Задача просрочена",
                    $"Задача «{task.Title}» просрочена.",
                    NotificationType.Task,
                    $"/tasks/{task.Id}",
                    task.Id.ToString());
            }
        }

        var projectsEndingTomorrow = await db.Projects
            .Where(p => p.EndDate.HasValue
                && p.EndDate.Value >= tomorrowUtcStart
                && p.EndDate.Value < dayAfterTomorrowUtcStart
                && p.Status == ProjectStatus.Active)
            .ToListAsync(ct);

        foreach (var project in projectsEndingTomorrow)
        {
            var alreadySent = await db.Notifications.AnyAsync(
                n => n.RelatedEntityId == project.Id.ToString()
                  && n.Title == "Срок проекта"
                  && n.CreatedAt >= todayUtcStart
                  && n.CreatedAt < tomorrowUtcStart,
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

        var nowUtc = DateTime.UtcNow;
        await SendEventReminderAsync(db, notif, nowUtc, 60, 55, 65, ct);
        await SendEventReminderAsync(db, notif, nowUtc, 30, 25, 35, ct);
    }

    private static async Task SendEventReminderAsync(
        AppDbContext db,
        INotificationService notif,
        DateTime nowUtc,
        int reminderMinutes,
        int windowStartMinutes,
        int windowEndMinutes,
        CancellationToken ct)
    {
        var windowStart = nowUtc.AddMinutes(windowStartMinutes);
        var windowEnd = nowUtc.AddMinutes(windowEndMinutes);

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

            var startLocal = KyrgyzstanTime.ConvertFromUtc(ev.StartTime);
            foreach (var userId in recipients)
            {
                var body = reminderMinutes == 60
                    ? $"Через 1 час: \"{ev.Title}\" в {startLocal:dd.MM.yyyy HH:mm}"
                    : $"Через 30 минут: \"{ev.Title}\" в {startLocal:dd.MM.yyyy HH:mm}";

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
