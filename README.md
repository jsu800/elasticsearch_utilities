# ElasticSearchUtilities
This repo contains a number of commonly used ElasticSearch applications as they apply to .NET in C#. The utilities and 
snippets make use of NEST (http://nest.azurewebsites.net/) and Elasticsearch.Net the two official clients for ElasticSearch. 

# Elastisearch.Net vs. NEST
Elasticsearch.Net is a very low level, dependency free, client that has no opinions about how you build and represent 
your requests and responses. It has abstracted enough so that all the Elasticsearch API endpoints are represented as 
methods but not too much to get in the way of how you want to build your json/request/response objects. It also comes 
with builtin, configurable/overridable, cluster failover retry mechanisms. Elasticsearch is elastic so why not your client?

NEST is a high level client that has the advantage of having mapped all the request and response objects, comes with a 
strongly typed query DSL that maps 1 to 1 with the Elasticsearch query DSL, and takes advantage of specific .NET features 
such as covariant results. NEST internally uses, and still exposes, the low level Elasticsearch.Net client.

Examples can be found:

* http://joelabrahamsson.com/extending-aspnet-mvc-music-store-with-elasticsearch/
* https://github.com/searchbox-io/.net-sample
