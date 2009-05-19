using System;
using System.IO;
using System.Text;
using System.Web;
using System.Xml.XPath;
using DotNetNuke.Entities.Modules;
using DotNetNuke.Services.Exceptions;
using Engage.Dnn.Publish.Portability;

using System.Collections;
using System.Collections.Specialized;
using System.Data;
using System.Globalization;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters;

using DotNetNuke.Common.Utilities;

using DotNetNuke.Services.Search;
using Engage.Dnn.Publish.Util;
using DotNetNuke.Entities.Tabs;


namespace Engage.Dnn.Publish.Util
{

    /// <summary>
    /// Features Controller Class supports IPortable currently.
    /// </summary>
    public class FeaturesController : IPortable, ISearchable
    {

        public FeaturesController()
        {

        }

        #region IPortable Members

        /// <summary>
        /// Method is invoked when portal template is imported or user selects Import Content from menu.
        /// </summary>
        /// <param name="ModuleID"></param>
        /// <param name="Content"></param>
        /// <param name="Version"></param>
        /// <param name="UserID"></param>
        public void ImportModule(int ModuleID, string Content, string Version, int UserID)
        {
     
            TransportableXmlValidator validator = new TransportableXmlValidator();
            MemoryStream stream = new MemoryStream(Encoding.UTF8.GetBytes(Content));

            if (validator.Validate(stream) == false)
            {
                Exception invalidXml = new Exception("Unable to import publish content due to incompatible XML file. Error: " + validator.Errors[0].ToString());
                Exceptions.LogException(invalidXml);
                throw invalidXml;
            }

            //The DNN ValidatorBase closes the stream? Must re-create. hk
            stream = new MemoryStream(Encoding.UTF8.GetBytes(Content));
            XPathDocument doc = new XPathDocument(stream);
            XmlTransporter builder = new XmlTransporter(ModuleID);

            try
            {
                XmlDirector.Deconstruct(builder, doc);
            }
            catch (Exception e)
            {
                Exceptions.LogException(new Exception(e.ToString()));
                throw;
            }
        }


        /// <summary>
        /// Method is invoked when portal template is created or user selects Export Content from menu.
        /// </summary>
        /// <param name="ModuleID"></param>
        /// <returns></returns>
        public string ExportModule(int ModuleID)
        {
            bool exportAll = false;

            //check query string for a "All" param to signal all rows, not just for a moduleId
            if (HttpContext.Current != null && HttpContext.Current.Request.QueryString["all"] != null)
            {
                exportAll = true;
            }
            XmlTransporter builder = null;
            try
            {
                builder = new XmlTransporter(ModuleID);
                XmlDirector.Construct(builder, exportAll);
            }
            catch (Exception e)
            {
                Exceptions.LogException(new Exception(e.ToString()));
                throw;
            }

            return builder.Document.OuterXml;
        }

        #endregion

        #region ISearchable Members

        public SearchItemInfoCollection GetSearchItems(ModuleInfo ModInfo)
        {
            SearchItemInfoCollection items = new SearchItemInfoCollection();
            AddArticleSearchItems(items, ModInfo);
            return items;
        }

        #endregion

        private static void AddArticleSearchItems(SearchItemInfoCollection items, ModuleInfo modInfo)
        {
            //get all the updated items
            //DataTable dt = Article.GetArticlesSearchIndexingUpdated(modInfo.PortalID, modInfo.ModuleDefID, modInfo.TabID);

            //TODO: we should get articles by ModuleID and only perform indexing by ModuleID 
            DataTable dt = Article.GetArticlesByModuleId(modInfo.ModuleID);
            SearchArticleIndex(dt, items, modInfo);

        }

        private static void SearchArticleIndex(DataTable dt, SearchItemInfoCollection items, ModuleInfo modInfo)
        {
            for (int i = 0; i < dt.Rows.Count; i++)
            {
                DataRow row = dt.Rows[i];
                StringBuilder searchedContent = new StringBuilder(8192);
                //article name
                string name = HtmlUtils.Clean(row["Name"].ToString().Trim(), false);

                if (Utility.HasValue(name))
                {
                    searchedContent.AppendFormat("{0}{1}", name, " ");
                }
                else
                {
                    //do we bother with the rest?
                    continue;
                }

                //article text
                string articleText = row["ArticleText"].ToString().Trim();
                if (Utility.HasValue(articleText))
                {
                    searchedContent.AppendFormat("{0}{1}", articleText, " ");
                }

                //article description
                string description = row["Description"].ToString().Trim();
                if (Utility.HasValue(description))
                {
                    searchedContent.AppendFormat("{0}{1}", description, " ");
                }

                //article metakeyword
                string keyword = row["MetaKeywords"].ToString().Trim();
                if (Utility.HasValue(keyword))
                {
                    searchedContent.AppendFormat("{0}{1}", keyword, " ");
                }

                //article metadescription
                string metaDescription = row["MetaDescription"].ToString().Trim();
                if (Utility.HasValue(metaDescription))
                {
                    searchedContent.AppendFormat("{0}{1}", metaDescription, " ");
                }

                //article metatitle
                string metaTitle = row["MetaTitle"].ToString().Trim();
                if (Utility.HasValue(metaTitle))
                {
                    searchedContent.AppendFormat("{0}{1}", metaTitle, " ");
                }

                string itemId = row["ItemId"].ToString();
                SearchItemInfo item = new SearchItemInfo();
                item.Title = name;
                item.Description = HtmlUtils.Clean(description, false);
                item.Author = Convert.ToInt32(row["AuthorUserId"], CultureInfo.InvariantCulture);
                item.PubDate = Convert.ToDateTime(row["LastUpdated"], CultureInfo.InvariantCulture);
                item.ModuleId = modInfo.ModuleID;
                item.SearchKey = "Article-" + itemId;
                item.Content = HtmlUtils.StripWhiteSpace(HtmlUtils.Clean(searchedContent.ToString(), false), true);
                item.GUID = "itemid=" + itemId;

                items.Add(item);

                //Check if the Portal is setup to enable venexus indexing
                if (ModuleBase.AllowVenexusSearchForPortal(modInfo.PortalID))
                {
                    string indexUrl = Utility.GetItemLinkUrl(Convert.ToInt32(itemId, CultureInfo.InvariantCulture), modInfo.PortalID, modInfo.TabID, modInfo.ModuleID);

                    //UpdateVenexusBraindump(IDbTransaction trans, string indexTitle, string indexContent, string indexWashedContent)
                    Data.DataProvider.Instance().UpdateVenexusBraindump(Convert.ToInt32(itemId, CultureInfo.InvariantCulture), name, articleText, HtmlUtils.Clean(articleText, false).ToString(), modInfo.PortalID, indexUrl);
                }
                //}
            }
        }
 
    }
}
