﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using StackExchange.Opserver.Data;
using StackExchange.Opserver.Data.Dashboard;
using TeamCitySharp.DomainEntities;

namespace StackExchange.Opserver.Controllers
{
    public partial class GraphController
    {
        [OutputCache(Duration = 120, VaryByParam = "id;start;end;summary", VaryByContentEncoding = "gzip;deflate")]
        [Route("graph/cpu/json")]
        public async Task<ActionResult> CPUJson(string id, long start, long end, bool? summary = false)
        {
            var node = DashboardData.GetNodeById(id);
            if (node == null) return JsonNotFound();
            var nodePoints = await node.GetCPUUtilization(start.ToDateTime(), end.ToDateTime(), 1000);
            if (nodePoints == null) return JsonNotFound();

            return Json(new
            {
                points = nodePoints.Select(p => new
                    {
                        date = p.DateEpoch, 
                        value = p.Value ?? 0
                    }),
                summary = summary.GetValueOrDefault(false) ? (await node.GetCPUUtilization(null, null, 2000)).Select(p => new
                    {
                        date = p.DateEpoch, 
                        value = p.Value ?? 0
                    }) : null,
                builds = !BuildStatus.HasCachePrimed ? null : GetBuilds(id, start, end).Select(b => new
                                                                {
                                                                    date = b.StartDate.ToEpochTime(true),
                                                                    text = GetFlagTooltip(b),
                                                                    link = b.WebUrl
                                                                })
            });
        }

        [OutputCache(Duration = 120, VaryByParam = "id;start;end;summary", VaryByContentEncoding = "gzip;deflate")]
        [Route("graph/memory/json")]
        public async Task<ActionResult> MemoryJson(string id, long start, long end, bool? summary = false)
        {
            var node = DashboardData.GetNodeById(id);
            if (node == null) return JsonNotFound();
            var detailPoints = await node.GetMemoryUtilization(start.ToDateTime(), end.ToDateTime(), 1000);
            if (detailPoints == null) return JsonNotFound();

            return Json(new
            {
                points = detailPoints.Select(p => new
                    {
                        date = p.DateEpoch,
                        value = (int)(p.Value / 1024 / 1024 ?? 0)
                    }),
                summary = summary.GetValueOrDefault(false) ? (await node.GetMemoryUtilization(null, null, 1000)).Select(p => new
                    {
                        date = p.DateEpoch,
                        value = (int)(p.Value / 1024 / 1024 ?? 0)
                    }) : null,
                builds = !BuildStatus.HasCachePrimed ? null : GetBuilds(id, start, end).Select(b => new
                {
                    date = b.StartDate.ToEpochTime(true),
                    text = GetFlagTooltip(b),
                    link = b.WebUrl
                })
            });
        }

        [OutputCache(Duration = 120, VaryByParam = "id;iid;start;end;summary", VaryByContentEncoding = "gzip;deflate")]
        [Route("graph/network/json")]
        public async Task<ActionResult> NetworkJson(string id, string iid, long start, long end, bool? summary = false)
        {
            var iface = DashboardData.GetNodeById(id)?.GetInterface(iid);
            if (iface == null) return JsonNotFound();
            var traffic = await iface.GetUtilization(start.ToDateTime(), end.ToDateTime(), 1000);
            if (traffic == null) return JsonNotFound();

            var anyTraffic = traffic.Any();

            return Json(new
                {
                    maximums = new
                        {
                            main_in = anyTraffic ? traffic.Max(i => (int)i.Value.GetValueOrDefault(0)) : 0,
                            main_out = anyTraffic ? traffic.Max(i => (int)i.BottomValue.GetValueOrDefault(0)) : 0
                        },
                    points = traffic.Select(i => new 
                        {
                            date = i.DateEpoch, 
                            main_in = (int)(i.Value.GetValueOrDefault()),
                            main_out = (int)(i.BottomValue.GetValueOrDefault())
                        }),
                    summary = summary.GetValueOrDefault()
                                  ? (await iface.GetUtilization(null, null, 2000)).Select(i => new
                                      {
                                          date = i.DateEpoch,
                                          main_in = (int)(i.Value.GetValueOrDefault()),
                                          main_out = (int)(i.BottomValue.GetValueOrDefault())
                                      })
                                  : null
                });
        }

        [OutputCache(Duration = 120, VaryByParam = "id;start;end", VaryByContentEncoding = "gzip;deflate")]
        [Route("graph/builds/json")]
        public ActionResult BuildsJson(string id, long start, long end)
        {
            return Json(new
            {
                builds = GetBuilds(id, start, end).Select(b => new
                {
                    date = b.StartDate.ToEpochTime(true),
                    text = GetFlagTooltip(b),
                    link = b.WebUrl
                })
            });
        }

        private static IEnumerable<Build> GetBuilds(string id, long startEpoch, long endEpoch)
        {
            if (!Current.Settings.TeamCity.Enabled) return Enumerable.Empty<Build>();

            // only show builds when zoomed in, say 5 days for starters?
            //TODO: Move this to a setting
            if((endEpoch - startEpoch) > TimeSpan.FromDays(30).TotalSeconds)
                return new List<Build>();

            var node = DashboardData.GetNodeById(id);
            DateTime start = startEpoch.ToDateTime(), end = endEpoch.ToDateTime();
            return BuildStatus.GetBuildsByServer(node.PrettyName).Where(b => b.StartDate >= start && b.StartDate <= end);
        }

        private static string GetFlagTooltip(Build b)
        {
            return $"{b.NiceProjectName()} - {b.NiceName()} #{b.Number}";
        }
    }
}