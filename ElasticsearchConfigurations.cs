uing System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ElasticSearchIndexer
{
    /// <summary>
    /// Configurations encompass definitions and declarations of all indices as well as network settings
    /// This is specific to .NET but can be extended to other language platforms
    /// </summary>
    public static class ElasticsearchConfigurations
    {

        public static readonly string ELASTICSEARCH_URL = ConfigurationManager.AppSettings["ElasticSearchUrl"];

        // This specifies the TTL of a scrolled view to persist in memory
        public static string SCAN_SEARCH_TTL = "3m";

        // This specifies the search size, returning the specified number of IDs 
        // per scroll. Total number of IDs returned is NUM_ID_PER_SCROLL_PER_SHARD * # of shards
        public static int NUM_ID_PER_SCROLL_PER_SHARD = 500;
    
        // Can set default index and type names heres

    }
}

