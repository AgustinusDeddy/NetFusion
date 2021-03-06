﻿using NetFusion.Bootstrap.Manifests;

namespace NetFusion.Utilities.Mapping
{
    public class Manifest : PluginManifestBase,
        ICorePluginManifest
    {
        public string PluginId => "{83C90E78-D245-4B0D-A4FC-E74B11227766}";
        public string Name => "Mapping Utility Plug-in";

        public string Description => 
            "Utility Plug-In used to map domain entities to other object representations.  " + 
            "This plug-in configures and coordinates the mapping process but does not dependent " +
            "on any specific mapping library.  The mapping library is specified by the host application.";
    }
}
