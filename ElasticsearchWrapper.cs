using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using System.Web.Hosting;
using Elasticsearch.Net;
using Nest;

namespace ElasticSearchIndexer
{
    /// <summary>
    /// This class wraps the most frequently used methods from Elasticsearch.NET and NEST which wrap 
    /// the native ES client in Lucene. One may reference the .NET source for frequent updates and modification of this class 
    /// from time to time:
    /// 
    /// https://github.com/elastic/elasticsearch-net
    /// 
    /// </summary>
    internal class ElasticsearchClientWrapper
    {
        private static volatile ElasticClient _client;
        private static object _clientLock = new Object();
        private static bool _isConnected = false;

        private static ElasticClient Instance
        {
            get
            {
                if (_client == null)
                {
                    lock (_clientLock)
                    {
                        if (_client == null)
                        {
                            Initialize();
                        }
                    }
                }
                return _client;
            }
        }

        /// <summary>
        /// This method is called upon once by the user to perform a few things:
        /// 1. Setting the connection configurations
        /// 2. Mapping the default types to indices, for example 'PromotionPOCO' type will be associated with the 'promotions' index
        /// 3. Creating a NEST ElasticClient instance, which is the net object for all net transactions
        /// 4. Making sure connection establishment succeeds
        /// </summary>
        private static void Initialize()
        {
            bool retVal = true;

            // initialize Elastic Search without any default index unless it's specified
            var settings = new ConnectionSettings(new Uri(ElasticsearchConfigurations.ELASTICSEARCH_URL))
                .MaximumRetries(3);

            // Index name inferences. Please add to this as new type is created
            // http://nest.azurewebsites.net/nest/index-type-inference.html
            settings.MapDefaultTypeIndices(d => d
                .Add(typeof (POCOOjbect1), ElasticsearchConfigurations.IndexNamePOCOObject1)
                .Add(typeof (POCOObject2), ElasticsearchConfigurations.IndexNamePOCOObject2)
                );

            // setting up the client for use
            _client = new ElasticClient(settings);

            // checking to see if setup has been successful
            if (_client.ClusterHealth().ConnectionStatus.Success == false)
            {
                System.Diagnostics.Debug.WriteLine("Connection Failed");
            }
            else
            {
                _isConnected = true;
            }
        }

        /// <summary>
        /// Return whether the singleton elasticsearch client is available or not
        /// </summary>
        /// <returns></returns>
        public static bool IsConnected()
        {
            return _isConnected;
        }

        /// <summary>
        ///  This method generates a unique index name based on the base index name
        /// </summary>
        /// <param name="defaultIndexName"></param>
        /// <returns></returns>
        public static string NewUniqueIndexSuffix(string defaultIndexName)
        {
            return defaultIndexName + "_" + Guid.NewGuid().ToString();
        }

        #region INDEX_MANAGEMENT

        /// <summary>
        /// Index a given POCO object of type T, to indexName (if available). If the indexName isn't available 
        /// the index operation defaults the object to the existing index mapped to the initial connection 
        /// settings namely:
        ///     settings.MapDefaultTypeIndices
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="object"></param>
        /// <param name="indexName"></param>
        /// <returns></returns>
        public static IIndexResponse Index<T>(T @object, string indexName = null) where T : class
        {
            return String.IsNullOrEmpty(indexName)
                ? Instance.Index<T>(@object)
                : Instance.Index<T>(@object, i => i
                    .Index(indexName));
        }

        /// <summary>
        /// This method is similar to the Index<T>() however indexing is idempotent and done via PUT.
        /// The POCO object is only created when absent otherwise the operation bypasses the already
        /// existing object in cache. This is useful in creating a brand new index (re-index) from scratch
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="object"></param>
        /// <param name="indexName"></param>
        /// <returns></returns>
        public static IIndexResponse IndexByPutIfAbsent<T>(T @object, string indexName = null) where T : class
        {
            return String.IsNullOrEmpty(indexName)
                ? Instance.Index<T>(@object, i => i
                    .OpType(OpType.Create))
                : Instance.Index<T>(@object, i => i
                    .Index(indexName)
                    .OpType(OpType.Create));
        }

        #endregion INDEX_MANAGEMENT

        #region SEARCH_SCROLL_MANAGEMENT

        public static ISearchResponse<T> Search<T>() where T : class
        {
            // Defaulting search size to 500 IDs per scroll. Total number of results 
            // is size * # of shards
            return Instance.Search<T>(s => s
                .From(0)
                .Size(ElasticsearchConfigurations.NUM_ID_PER_SCROLL_PER_SHARD)
                .MatchAll()
                .SearchType(SearchType.Scan)
                .Scroll(ElasticsearchConfigurations.SCAN_SEARCH_TTL)
                );
        }

        public static ISearchResponse<T> Search<T>(string indexName) where T : class
        {
            return Instance.Search<T>(s => s
                .From(0)
                .Size(ElasticsearchConfigurations.NUM_ID_PER_SCROLL_PER_SHARD)
                .Index(indexName)
                .SearchType(SearchType.Scan)
                .Scroll(ElasticsearchConfigurations.SCAN_SEARCH_TTL)
                .MatchAll()
                );
        }

        public static ISearchResponse<T> Scroll<T>(string scrollId) where T : class
        {
            return Instance.Scroll<T>(ElasticsearchConfigurations.SCAN_SEARCH_TTL, scrollId);
        }

        #endregion

        #region ALIAS_MANAGEMENT


        /// <summary>
        /// Search and return a list of existing aliases associated with a particular index
        /// </summary>
        /// <param name="indexName"></param>
        /// <returns></returns>
        public static IGetAliasesResponse GetAliases(string indexName)
        {
            return Instance.GetAliases(a => a.Index(indexName));
        }

        /// <summary>
        /// Add a new index to an alias, either or both can be new
        /// </summary>
        /// <param name="aliasName"></param>
        /// <param name="indexName"></param>
        public static void CreateAlias(string aliasName, string indexName)
        {
            Instance.Alias(a => a.Add(
                add => add
                    .Index(indexName)
                    .Alias(aliasName))
                );
        }

        /// <summary>
        /// Delete an existing alias but NOT the index it points to
        /// </summary>
        /// <param name="aliasName"></param>
        /// <param name="indexName"></param>
        public static void DeleteAlias(string aliasName, string indexName)
        {
            Instance.Alias(a => a.Remove((
                remove => remove
                    .Index(indexName)
                    .Alias(aliasName)
                ))
                );
        }

        /// <summary>
        /// This method combines both AddAlias() and DeleteAlias() in one. To reroute an 
        /// alias from an existing index to a new index, we perform both Add and Remove  
        /// in the same operation to atomically reroute alias
        /// </summary>
        /// <param name="aliasName"></param>
        /// <param name="oldIndexName"></param>
        /// <param name="newIndexName"></param>
        public void RerouteAlias(string aliasName, string oldIndexName, string newIndexName)
        {
            Instance.Alias(a => a
                .Add(add => add
                    .Alias(aliasName)
                    .Index(newIndexName)
                )
                .Remove(remove => remove
                    .Alias(aliasName)
                    .Index(oldIndexName)
                )
                );
        }

        #endregion ALIAS_MANAGEMENT

        #region DELETION_MANAGEMENT

        /// <summary>
        /// This method deletes any giving document id from cache. Document id is a string but it can 
        /// also be an int and then id.ToString()
        /// </summary>
        public static bool DeleteIdFromCache<T>(String id) where T : class
        {
            bool retVal = true;
            try
            {
                Instance.Delete<T>(id);
            }
            catch (Exception e)
            {
                System.Diagnostics.Debug.WriteLine("Exception Occurred: " + e.Message + ": " + e.StackTrace);
                retVal = false;
            }
            return retVal;
        }

        #endregion

        #region COUNT_MANAGEMENT

        /// <summary>
        /// This method returns the total document count per the specified index and type as indicated by the 
        /// generic T
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static ICountResponse GetDocumentCountPerIndexAndType<T>() where T : class
        {
            return Instance.Count<T>(c => c.Query(q => q.MatchAll()));
        }

        #endregion COUNT_MANAGEMENT
    }

}

