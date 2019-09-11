namespace Sitecore.Foundation.MultiSiteAliases.Model
{
    using Sitecore;
    using Sitecore.Data;
    using Sitecore.Data.Fields;
    using Sitecore.Data.Items;
    using Sitecore.Diagnostics;
    using Sitecore.Text;
    using System;
    using System.Collections.Generic;
    using System.Linq;

    internal class AliasInfo
    {
        public AliasInfo() : base()
        {

        }
        public readonly ListString path;
        public AliasInfo(string value)
        {
            Assert.ArgumentNotNullOrEmpty(value, nameof(value));
            value = StringUtil.RemovePrefix(MultiSiteAliases.Constants.ForwardSlash, value);
            value = StringUtil.RemovePostfix(MultiSiteAliases.Constants.ForwardSlash, value);
            this.path = new ListString(value, MultiSiteAliases.Constants.ForwardSlashChar);
        }
        public IEnumerable<string> Ascenders
        {
            get
            {
                if (this.path.Count > 1)
                {
                    for (int i = 0; i < this.path.Count - 1; ++i)
                        yield return this.path[i];
                }
            }
        }
        public IEnumerable<string> AscendersAndName
        {
            get
            {
                return (IEnumerable<string>)this.path.Items;
            }
        }
        public string AliasesNameWithPath
        {
            get
            {
                return string.Join(MultiSiteAliases.Constants.ForwardSlash, AscendersAndName);
            }
        }
        public string Name
        {
            get
            {
                return this.path[this.path.Count - 1];
            }
        }
        public CustomAliases AliasesWindow(Item item)
        {
            var customAliases = new CustomAliases();
            //ID itemId = new ID("{110D559F-DEA5-42EA-9C1C-8A5DF7E70EF9}");//TODO: map from parameter

            customAliases.ItemId = item.ID;
            var selectedItemFullPath = item.Paths.FullPath;

            foreach (var site in Sitecore.Sites.SiteManager.GetSites())
            {
                var destination = site?.Properties[MultiSiteAliases.Constants.SiteProperties.SiteLevelAliases];
                if (!string.IsNullOrEmpty(destination) && ID.TryParse(destination, out ID id) && !id.IsNull && id.Guid != Guid.Empty)
                {
                    if (selectedItemFullPath.StartsWith(site.Properties[MultiSiteAliases.Constants.SiteProperties.RootPath])
                                || selectedItemFullPath.StartsWith(MultiSiteAliases.Constants.MediaPath))
                        ProcessEachSite(item, customAliases, site, id);
                }
            }

            return customAliases;
        }

        private static void ProcessEachSite(Item item, CustomAliases customAliases, Sitecore.Sites.Site site, ID id)
        {
            var customAliasesFolderItem = Context.ContentDatabase.GetItem(id);
            if (customAliasesFolderItem != null)
            {
                //To Show in Multi List for user
                customAliases.AllSites.Add(new Sites() { SiteName = site.Name, AliasesPathId = id });

                //To Show Aliases as same Url pattern
                var roothFullPath = customAliasesFolderItem.Paths.FullPath + MultiSiteAliases.Constants.ForwardSlash;

                var itemId = item.ID;
                foreach (var eachAliases in customAliasesFolderItem
                                            .Axes.GetDescendants()
                                            .Where(f => f.TemplateID == MultiSiteAliases.Constants.Template.AliasesTemplateId))
                {
                    LinkField lnkField = eachAliases.Fields[MultiSiteAliases.Constants.Fields.LinkedField];
                    if (lnkField?.TargetID == itemId)
                    {
                        var path = eachAliases.Paths.FullPath.Replace(roothFullPath, string.Empty);
                        //To Show as already Exists Entry
                        customAliases.AddedSites.Add(new Sites()
                        {
                            SiteName = site.Name,
                            AliasesPathId = id,
                            AliasesName = path,
                            AliasesItemId = eachAliases.ID
                        });
                    }
                }
            }
        }
    }
}