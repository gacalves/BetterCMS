﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Mvc;

using BetterCms.Core.Exceptions.Mvc;

using BetterCms.Module.Root.Content.Resources;
using BetterCms.Module.Root.Models;
using BetterCms.Module.Root.Mvc.Helpers;
using BetterCms.Module.Root.Projections;
using BetterCms.Module.Root.ViewModels.Cms;
using BetterCms.Module.Root.ViewModels.Content;
using BetterCms.Module.Root.Views.Language;

using HtmlAgilityPack;

namespace BetterCms.Module.Root.Mvc.PageHtmlRenderer
{
    public class ChildContentRenderHelper
    {
        /// <summary>
        /// The regex pattern for to find all child widgets in the content
        /// </summary>
        public const string ChildWidgetRegexPattern = "<widget[^>]*>.*?<\\/widget>";

        /// <summary>
        /// The widget identifier attribute name
        /// </summary>
        public const string WidgetIdAttributeName = "data-id";

        /// <summary>
        /// The widget assignment identifier attribute name
        /// </summary>
        public const string WidgetAssignmentIdAttributeName = "data-assign-id";

        /// <summary>
        /// The HTML helper
        /// </summary>
        private readonly HtmlHelper htmlHelper;

        /// <summary>
        /// Initializes a new instance of the <see cref="ChildContentRenderHelper"/> class.
        /// </summary>
        /// <param name="htmlHelper">The HTML helper.</param>
        public ChildContentRenderHelper(HtmlHelper htmlHelper)
        {
            this.htmlHelper = htmlHelper;
        }

        public StringBuilder AppendHtml(StringBuilder stringBuilder, PageContentProjection projection, RenderPageViewModel pageModel)
        {
            var renderingPageModel = pageModel.RenderingPage ?? pageModel;
            var content = projection.GetHtml(htmlHelper);

            var childrenContents = projection.GetChildProjections() ?? new List<ChildContentProjection>();
            var parsedWidgets = ParseWidgetsFromHtml(content).Distinct();

            var availableWidgets = childrenContents.Where(cc => parsedWidgets.Any(id => id.AssignmentIdentifier == cc.AssignmentIdentifier));
            foreach (var childProjection in availableWidgets)
            {
                var model = parsedWidgets.First(w => w.AssignmentIdentifier == childProjection.AssignmentIdentifier);
                var replaceWhat = model.Match.Value;
                var replaceWith = AppendHtml(new StringBuilder(), childProjection, renderingPageModel).ToString();

                content = content.Replace(replaceWhat, replaceWith);
            }

            // Widgets, which has no access (e.g. widgets with draft status for public users)
            var invisibleWidgets = parsedWidgets.Where(id => childrenContents.All(cc => cc.AssignmentIdentifier != id.AssignmentIdentifier));
            foreach (var model in invisibleWidgets)
            {
                var replaceWhat = model.Match.Value;
                var replaceWith = string.Empty;

                content = content.Replace(replaceWhat, replaceWith);

            }

            // Add child contents in the master page to child region is possible only if content is widget.
            // If content is regulat HTML content, it works as master page contents, and contens may be added only in the child page
            if (!renderingPageModel.IsMasterPage || projection.Content is IChildRegionContainer)
            {
                content = AppendHtmlWithChildRegionContens(content, projection, renderingPageModel);
            }

            stringBuilder.Append(content);

            return stringBuilder;
        }

        public static List<ChildContentModel> ParseWidgetsFromHtml(string searchIn, bool throwException = false)
        {
            if (string.IsNullOrWhiteSpace(searchIn))
            {
                return new List<ChildContentModel>(0);
            }

            var result = new List<ChildContentModel>();

            var matches = Regex.Matches(searchIn, ChildWidgetRegexPattern, RegexOptions.IgnoreCase);
            foreach (Match match in matches)
            {
                var model = new ChildContentModel();
                var htmlDocument = new HtmlDocument();
                htmlDocument.LoadHtml(match.Value);

                var widgetNode = htmlDocument.DocumentNode.Descendants("widget").First();
                var widgetIdAttribute = widgetNode.Attributes.FirstOrDefault(a => a.Name == WidgetIdAttributeName);
                Guid widgetId;
                if (widgetIdAttribute == null || string.IsNullOrWhiteSpace(widgetIdAttribute.Value) || !Guid.TryParse(widgetIdAttribute.Value, out widgetId))
                {
                    if (throwException)
                    {
                        var message = RootGlobalization.ChildContent_FailedToParseWidgetId_Message;
                        throw new ValidationException(() => message, message);
                    }

                    continue;
                }
                model.WidgetId = widgetId;
                model.Title = htmlDocument.DocumentNode.InnerText;
                model.WidgetHtmlNode = widgetNode;
                model.Match = match;

                var assignmentIdAttribute = widgetNode.Attributes.FirstOrDefault(a => a.Name == WidgetAssignmentIdAttributeName);
                Guid assignmentId;
                if (assignmentIdAttribute == null || string.IsNullOrWhiteSpace(assignmentIdAttribute.Value) || !Guid.TryParse(assignmentIdAttribute.Value, out assignmentId))
                {
                    if (throwException)
                    {
                        var message = RootGlobalization.ChildContent_FailedToParseAssignmentId_MEssage;
                        throw new ValidationException(() => message, message);
                    }

                    continue;
                }
                model.AssignmentIdentifier = assignmentId;

                result.Add(model);
            }

            return result;
        }

        private string AppendHtmlWithChildRegionContens(string html, PageContentProjection projection, RenderPageViewModel pageModel)
        {
            // Render contents from children regions
            var childRegionContents = projection.GetChildRegionContentProjections() ?? new List<PageContentProjection>();
            if (projection.Content is IChildRegionContainer
                && projection.Content.ContentRegions != null
                && projection.Content.ContentRegions.Any())
            {
                var stringBuilder = new StringBuilder(html);
                var pageHtmlHelper = new PageHtmlRenderer(stringBuilder, pageModel);

                foreach (var region in projection.Content.ContentRegions)
                {
                    var contentsBuilder = new StringBuilder();
                    var regionModel = new PageRegionViewModel
                        {
                            RegionId = region.Region.Id,
                            RegionIdentifier = region.Region.RegionIdentifier
                        };
                    var childRegionContentProjections = childRegionContents.Where(c => c.RegionId == regionModel.RegionId).OrderBy(c => c.Order).ToList();

                    using (new LayoutRegionWrapper(contentsBuilder, regionModel, pageModel.AreRegionsEditable))
                    {
                        foreach (var childRegionContentProjection in childRegionContentProjections)
                        {
                            // Add Html
                            using (new RegionContentWrapper(contentsBuilder, childRegionContentProjection, pageModel.CanManageContent && pageModel.AreRegionsEditable))
                            {
                                // Pass current model as view data model
                                htmlHelper.ViewData.Model = pageModel;

                                contentsBuilder = AppendHtml(contentsBuilder, childRegionContentProjection, pageModel);
                            }
                        }
                    }

                    // Insert region to master page
                    var regionHtml = contentsBuilder.ToString();
                    pageHtmlHelper.ReplaceRegionHtml(regionModel.RegionIdentifier, regionHtml);
                }

                if (pageModel.AreRegionsEditable)
                {
                    pageHtmlHelper.ReplaceRegionRepresentationHtml();
                }
                return pageHtmlHelper.GetReplacedHtml().ToString();
            }

            return html;
        }
    }
}