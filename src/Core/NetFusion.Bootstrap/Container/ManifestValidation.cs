﻿using NetFusion.Bootstrap.Exceptions;
using NetFusion.Bootstrap.Manifests;
using NetFusion.Common.Extensions;
using NetFusion.Common.Extensions.Collection;
using System;
using System.Collections.Generic;
using System.Linq;

namespace NetFusion.Bootstrap.Container
{
    /// <summary>
    /// Validates that the manifest registry was correctly constructed from
    /// the discovered assemblies representing plug-ins. 
    /// </summary>
    public class ManifestValidation
    {
        private readonly ManifestRegistry _registry;

        public ManifestValidation(ManifestRegistry registry)
        {
            _registry = registry;
        }

        public void Validate()
        {
            AssertManifestIds();
            AssertManifestNames();
            AssertLoadedManifests();
        }

        private void AssertManifestIds()
        {
            IEnumerable<Type> invalidManifestTypes = _registry.AllManifests
                .Where(m => m.PluginId.IsNullOrWhiteSpace())
                .Select(m => m.GetType());

            if (invalidManifestTypes.Any())
            {
                throw new ContainerException(
                    "All manifest instances must have a PluginId specified.  " +
                    "See details for invalid manifest types.", invalidManifestTypes);
            }

            IEnumerable<string> duplicateManifestIds = _registry.AllManifests
                .WhereDuplicated(m => m.PluginId);

            if (duplicateManifestIds.Any())
            {
                throw new ContainerException(
                    "Plug-in identity values must be unique.  See details for duplicated Plug-in Ids.",
                    duplicateManifestIds);
            }
        }

        private void AssertManifestNames()
        {
            IEnumerable<Type> invalidManifestTypes = _registry.AllManifests
                .Where(m => m.AssemblyName.IsNullOrWhiteSpace() || m.Name.IsNullOrWhiteSpace())
                .Select(m => m.GetType());

            if (invalidManifestTypes.Any())
            {
                throw new ContainerException(
                    "All manifest instances must have AssemblyName and Name values.  " +
                    "See details for invalid manifest types.", invalidManifestTypes);
            }

            IEnumerable<string> duplicateNames = _registry.AllManifests.WhereDuplicated(m => m.Name);

            if (duplicateNames.Any())
            {
                throw new ContainerException(
                    "Plug-in names must be unique.  See details for duplicated Plug-in names.",
                    duplicateNames);
            }
        }

        private void AssertLoadedManifests()
        {
            if (_registry.AppHostPluginManifests.Empty())
            {
                throw new ContainerException(
                    $"An application plug-in manifest could not be found " +
                    $"derived from: {typeof(IAppHostPluginManifest)}");
            }

            if (!_registry.AppHostPluginManifests.IsSingletonSet())
            {
                throw new ContainerException(
                    "More than one application plug-in manifest was found.",
                    _registry.AppHostPluginManifests.Select(am => new
                    {
                        ManifestType = am.GetType().FullName,
                        am.AssemblyName,
                        PluginName = am.Name,
                        am.PluginId
                    }));
            }
        }
    }
}
