﻿using NetFusion.Bootstrap.Container;
using NetFusion.WebApi.Metadata;
using System.Collections.Generic;
using System.Web.Http;

namespace Samples.WebHost.Controllers
{
    [EndpointMetadata(EndpointName = "WebApiSamples", IncluedAllRoutes = true)]
    [RoutePrefix("api/samples/web")]
    public class WebApiSampleController : ApiController
    {
        private readonly IRouteMetadataService _routeMetadataSrv;

        public WebApiSampleController(IRouteMetadataService routeMetadataSrv)
        {
            _routeMetadataSrv = routeMetadataSrv;
        }

        /// <summary>
        /// Exposes all the controller endpoints as meta-data to the client.  A client side JS proxy
        /// can be written to use this metadata tomake requests without having hard-coded URLs.  In
        /// this case, all of the routes for the examples are exposed.
        /// </summary>
        [AllowAnonymous, HttpGet, Route("routes")]
        [RouteMetadata(IncludeRoute = false)]
        public IDictionary<string, EndpointMetadata> GetRoutes()
        {
            return _routeMetadataSrv.GetApiMetadata();
        }

        [HttpGet Route("composite/log")]
        public IDictionary<string, object> GetCompositeConfig()
        {
            return AppContainer.Instance.Log;
        }
    }
}