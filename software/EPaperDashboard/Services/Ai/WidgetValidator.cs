using System.Text.Json;
using EPaperDashboard.Models;
using static EPaperDashboard.Services.Ai.JsonElementHelpers;

namespace EPaperDashboard.Services.Ai;

public sealed class WidgetValidator(ILogger<WidgetValidator> logger)
{
    public List<WidgetConfig> ValidateAndRepair(
        List<WidgetConfig> widgets,
        AiDataSnapshot aiData,
        Dashboard dashboard)
    {
        var result = new List<WidgetConfig>();
        var initialCount = widgets.Count;

        foreach (var widget in widgets)
        {
            var repaired = ValidateWidget(widget, aiData, dashboard);
            if (repaired != null)
            {
                result.Add(repaired);
            }
        }

        if (result.Count < initialCount)
        {
            logger.LogInformation(
                "Widget validation removed {Removed} of {Total} widgets for dashboard {DashboardId}",
                initialCount - result.Count, initialCount, dashboard.Id);
        }

        return result;
    }

    private WidgetConfig? ValidateWidget(WidgetConfig widget, AiDataSnapshot aiData, Dashboard dashboard)
    {
        var config = widget.Config;

        switch (widget.Type)
        {
            case "calendar":
            {
                var entityId = GetStringProp(config, "entityId");
                if (string.IsNullOrEmpty(entityId))
                {
                    logger.LogWarning("Dropping calendar widget '{Id}': missing entityId", widget.Id);
                    return null;
                }
                if (!aiData.CalendarEvents.ContainsKey(entityId))
                {
                    logger.LogWarning("Dropping calendar widget '{Id}': entityId '{EntityId}' not in available data", widget.Id, entityId);
                    return null;
                }
                break;
            }

            case "todo":
            {
                var entityId = GetStringProp(config, "entityId");
                if (string.IsNullOrEmpty(entityId))
                {
                    logger.LogWarning("Dropping todo widget '{Id}': missing entityId", widget.Id);
                    return null;
                }
                if (!aiData.TodoItems.ContainsKey(entityId))
                {
                    logger.LogWarning("Dropping todo widget '{Id}': entityId '{EntityId}' not in available data", widget.Id, entityId);
                    return null;
                }
                break;
            }

            case "weather":
            {
                var entityId = GetStringProp(config, "entityId");
                if (string.IsNullOrEmpty(entityId))
                {
                    logger.LogWarning("Dropping weather widget '{Id}': missing entityId", widget.Id);
                    return null;
                }
                if (!aiData.EntityStates.ContainsKey(entityId))
                {
                    logger.LogWarning("Dropping weather widget '{Id}': entityId '{EntityId}' not in available data", widget.Id, entityId);
                    return null;
                }
                break;
            }

            case "weather-forecast":
            {
                var entityId = GetStringProp(config, "entityId");
                if (string.IsNullOrEmpty(entityId))
                {
                    logger.LogWarning("Dropping weather-forecast widget '{Id}': missing entityId", widget.Id);
                    return null;
                }
                if (!aiData.WeatherForecasts.ContainsKey(entityId))
                {
                    logger.LogWarning("Dropping weather-forecast widget '{Id}': entityId '{EntityId}' not in available data", widget.Id, entityId);
                    return null;
                }
                break;
            }

            case "rss-feed":
            {
                var entityId = GetStringProp(config, "entityId");
                if (string.IsNullOrEmpty(entityId))
                {
                    logger.LogWarning("Dropping rss-feed widget '{Id}': missing entityId", widget.Id);
                    return null;
                }
                if (!aiData.RssFeedEntries.ContainsKey(entityId))
                {
                    logger.LogWarning("Dropping rss-feed widget '{Id}': entityId '{EntityId}' not in available data", widget.Id, entityId);
                    return null;
                }
                break;
            }

            case "graph":
            {
                if (!config.TryGetProperty("series", out var seriesEl)
                    || seriesEl.ValueKind != JsonValueKind.Array
                    || seriesEl.GetArrayLength() == 0)
                {
                    logger.LogWarning("Dropping graph widget '{Id}': missing or empty series", widget.Id);
                    return null;
                }
                var hasValidSeries = false;
                foreach (var s in seriesEl.EnumerateArray())
                {
                    var eid = s.TryGetProperty("entityId", out var eidEl) ? eidEl.GetString() : null;
                    if (!string.IsNullOrEmpty(eid) && aiData.EntityStates.ContainsKey(eid))
                    {
                        hasValidSeries = true;
                        break;
                    }
                }
                if (!hasValidSeries)
                {
                    logger.LogWarning("Dropping graph widget '{Id}': no series entity IDs match available data", widget.Id);
                    return null;
                }
                break;
            }

            case "markdown":
            {
                var content = GetStringProp(config, "content");
                if (string.IsNullOrWhiteSpace(content))
                {
                    logger.LogWarning("Dropping markdown widget '{Id}': empty content", widget.Id);
                    return null;
                }
                break;
            }

            case "header":
            {
                var title = GetStringProp(config, "title");
                if (string.IsNullOrWhiteSpace(title))
                {
                    logger.LogInformation("Repairing header widget '{Id}': setting title to dashboard name", widget.Id);
                    widget.Config = JsonSerializer.SerializeToElement(
                        PatchJsonObject(config, "title", dashboard.Name));
                }
                break;
            }
        }

        return widget;
    }
}
