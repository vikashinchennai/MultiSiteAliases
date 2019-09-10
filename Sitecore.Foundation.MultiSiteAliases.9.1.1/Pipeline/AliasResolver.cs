namespace Sitecore.Foundation.MultiSiteAliases.Pipeline
{
    using Microsoft.Extensions.DependencyInjection;
    using Sitecore;
    using Sitecore.Abstractions;
    using Sitecore.Configuration;
    using Sitecore.Data;
    using Sitecore.Data.Items;
    using Sitecore.DependencyInjection;
    using Sitecore.Diagnostics;
    using Sitecore.Pipelines.HttpRequest;
    using Sitecore.Pipelines.HttpRequest.HandlerMapping;
    using Sitecore.Web;
    using System;
    public class AliasResolver : HttpRequestProcessor
    {
        private readonly HandlerMapper handlerMapper;

        public BaseMediaManager MediaManager { get; private set; }
               
        public bool AliasesActive { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="T:Sitecore.Pipelines.HttpRequest.AliasResolver" /> class.
        /// </summary>
        public AliasResolver()
          : this((HandlerMapper)new DirectHandlerMapper(ServiceLocator.ServiceProvider.GetRequiredService<BaseFactory>().GetCustomHandlers())
                                , ServiceLocator.ServiceProvider.GetRequiredService<BaseMediaManager>(), Settings.AliasesActive)
        {
        }

        internal AliasResolver(
          HandlerMapper handlerMapper,
          BaseMediaManager mediaManager,
          bool aliasesActive)
        {
            Assert.ArgumentNotNull((object)handlerMapper, nameof(handlerMapper));
            Assert.ArgumentNotNull((object)mediaManager, nameof(mediaManager));
            this.MediaManager = mediaManager;
            this.AliasesActive = aliasesActive;
            this.handlerMapper = handlerMapper;
        }


        private string AliasItemPath { get; set; }
        public override void Process(HttpRequestArgs args)
        {
            Assert.ArgumentNotNull(args, MultiSiteAliases.Constants.Args);
            if (!Settings.AliasesActive)
            {
                Tracer.Warning(MultiSiteAliases.Constants.AliasesNotActive);
                return;
            }
            Database database = Context.Database;
            if (database == null)
            {
                Tracer.Warning(MultiSiteAliases.Constants.ContextDatabaseNotFound);
                return;
            }

            Profiler.StartOperation(MultiSiteAliases.Constants.ResolveAliases);

            var customFolderItemId = Context.Site?.Properties[MultiSiteAliases.Constants.SiteProperties.SiteLevelAliases];

            if (!ID.TryParse(customFolderItemId, out ID folderItemId) || folderItemId.Guid == Guid.Empty)
                return;

            var item = database.GetItem(folderItemId);

            if (item == null || !item.HasChildren || item.Paths == null)
                return;

            string aliasSubFolderName = item.Paths?.FullPath;

            if (!string.IsNullOrEmpty(aliasSubFolderName))
            {
                aliasSubFolderName = aliasSubFolderName.Replace(MultiSiteAliases.Constants.AliasesRootPath, "");
            }

            //("Please Navigate to Settings->Aliases");
            AliasItemPath = aliasSubFolderName + args.LocalPath;
            if (database.Aliases.Exists(AliasItemPath))
            {
                if (this.ProcessItem(args))
                {
                    Item obj = Context.Item;
                    if (obj != null && obj.Paths != null && obj.Paths.IsMediaItem && obj.TemplateID != TemplateIDs.MediaFolder)
                    {
                        //call base Resolver method to handle Media type
                        ServiceLocator.ServiceProvider
                                    .GetRequiredService<Sitecore.Pipelines.HttpRequest.AliasResolver>()
                                    .Process(args);

                        string mediaUrl = MediaManager.GetMediaUrl((MediaItem)obj);
                        CustomHandler matchingHandler;
                        if (!string.IsNullOrEmpty(mediaUrl) && handlerMapper.TryFindMatchingHandler(mediaUrl, out matchingHandler))
                        {
                           // args.LocalPath = "asd";
                           // args.SitecoreContext.Data.RawUrl = mediaUrl;
                            args.HttpContext.RewritePath(matchingHandler.Handler, mediaUrl, args.Url.QueryString, true);
                            args.AbortPipeline();
                        }
                    }
                }
                //To Redirect External Site
                else if (this.ProcessExternalUrl(args))
                {
                    string path = Context.Page?.FilePath;
                    if (!string.IsNullOrEmpty(path) && WebUtil.IsExternalUrl(path))
                        WebUtil.Redirect(path);
                }

            }
            Profiler.EndOperation();
        }

        private bool ProcessExternalUrl(HttpRequestArgs args)
        {
            string targetUrl = Context.Database.Aliases.GetTargetUrl(AliasItemPath);
            if (targetUrl.Length > 0)
                return this.ProcessExternalUrl(targetUrl);
            return false;
        }
        private bool ProcessExternalUrl(string path)
        {
            if (Context.Page.FilePath.Length > 0)
                return false;
            Context.Page.FilePath = path;
            return true;
        }

        private bool ProcessItem(HttpRequestArgs args)
        {
            // Use the AliasItemPath property to find the site specific aliases
            ID targetID = Context.Database.Aliases.GetTargetID(AliasItemPath);
            if (!targetID.IsNull)
            {
                Item item = args.GetItem(targetID);
                if (item != null)
                {
                    this.ProcessItem(args, item);
                }
                return true;
            }
            // Use the AliasItemPath property to output porper information
            Tracer.Error(string.Format(MultiSiteAliases.Constants.AliasesNotLinked, AliasItemPath));
            return false;
        }

        private void ProcessItem(HttpRequestArgs args, Item target)
        {
            if (Context.Item == null)
            {
                Context.Item = target;
            }
        }
    }
}