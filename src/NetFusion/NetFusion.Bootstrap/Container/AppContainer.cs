﻿using Autofac;
using NetFusion.Bootstrap.Exceptions;
using NetFusion.Bootstrap.Logging;
using NetFusion.Bootstrap.Manifests;
using NetFusion.Bootstrap.Plugins;
using NetFusion.Common;
using NetFusion.Common.Extensions;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;

namespace NetFusion.Bootstrap.Container
{
    /// <summary>
    /// Responsible for bootstrapping an application composed from plug-in components.  
    /// After the application container is created, services registered by the plug-in 
    /// modules, can be accessed using the configured Autofac dependency injection container. 
    /// </summary>
    public class AppContainer : IAppContainer,
        ILoadedContainer
    {
        // Singleton instance of created container.
        private static AppContainer _instance;
        private bool _disposed = false;

        private readonly ITypeResolver _typeResover;
        private readonly Dictionary<Type, IContainerConfig> _configs;

        // Logging:
        private LoggerConfig _loggerConfig;
        private IContainerLogger _logger;
        private AppContainerLog _containerLog;

        // Contains references to all the discovered plug-in manifests
        // used to bootstrap the container.
        private ManifestRegistry Registry { get; }

        private readonly CompositeApplication _application;
        private Autofac.IContainer _container;
       
        internal AppContainer(string[] searchPatterns, ITypeResolver typeResolver)
        {
            _application = new CompositeApplication(searchPatterns);
            _configs = new Dictionary<Type, IContainerConfig>();

            _typeResover = typeResolver;
            _instance = this;

            this.Registry = new ManifestRegistry(); 
        }

        internal CompositeApplication CompositeApplication
        {
            get { return _application; }
        }

        // Log of the composite application structure showing how it was constructed.
        public IDictionary<string, object> Log
        {
            get
            {
                ThrowIfDisposed(this);
                return _containerLog?.GetLog();
            }
        }

        public static IAppContainer Instance
        {
            get
            {
                ThrowIfDisposed(AppContainer._instance);
                return _instance;
            }
        }

        public IContainerLogger Logger
        {
            get
            {
                ThrowIfDisposed(this);
                return _logger;
            }
        }

        // The created dependency-injection container.
        public IContainer Services
        {
            get
            {
                ThrowIfDisposed(this);
                return _container;
            }
        }

        /// <summary>
        /// Creates an application container using assemblies containing plug-ins
        /// from the host's execution directory matching the specific search patterns.
        /// </summary>
        /// <param name="searchPatterns">The search patterns used to specify the 
        /// assemblies that should be searched for plug-ins.</param>
        /// <returns>Configured application container.</returns>
        public static IAppContainer Create(string[] searchPatterns)
        {
            Check.NotNull(searchPatterns, nameof(searchPatterns), "search patterns must be specified");

            if (_instance != null)
            {
                throw new ContainerException("container has already been created");
            }

            searchPatterns = AddDefaultSearchPatterns(searchPatterns);

            var typeResolver = new TypeResolver(searchPatterns);
            return new AppContainer(searchPatterns, typeResolver);
        }

        /// <summary>
        /// Creates an application container using the types provided by the 
        /// specified type resolver.
        /// </summary>
        /// <param name="typeResolver">Reference to a custom type resolver.</param>
        /// <returns>Configured application container.</returns>
        public static IAppContainer Create(ITypeResolver typeResolver)
        {
            Check.NotNull(typeResolver, nameof(ITypeResolver), "type resolver must be specified");

            if (_instance != null)
            {
                throw new ContainerException("container has already been created");
            }

            return new AppContainer(new string[] { }, typeResolver);
        }

        public IAppContainer WithConfig(IContainerConfig config)
        {
            Check.NotNull(config, nameof(config), "configuration not specified");

            if (this.Registry.AllManifests != null)
            {
                throw new ContainerException("container has already been built.");
            }

            var configType = config.GetType();
            if (_configs.ContainsKey(configType))
            {
                throw new ContainerException(
                    $"existing configuration of type: {config.GetType()} is already configured");
            }

            _configs[configType] = config;
            return this;
        }

        public IAppContainer WithConfig<T>()
            where T : IContainerConfig, new()
        {
            WithConfig(new T());
            return this;
        }

        public IAppContainer WithConfig<T>(Action<T> configAction)
            where T : IContainerConfig, new()
        {
            Check.NotNull(configAction, nameof(configAction), "configuration delegate not specified");

            T config = new T();
            WithConfig(config);

            configAction(config);
            return this;
        }

        public IAppContainer WithConfigSection(params string[] sectionNames)
        {
            Check.NotNull(sectionNames, nameof(sectionNames), "section names must be specified");

            foreach (string sectionName in sectionNames)
            {
                var config = ConfigurationManager.GetSection(sectionName);
                if (config == null) continue;

                var containerConfig = config as IContainerConfig;
                if (containerConfig != null)
                {
                    this.WithConfig(containerConfig);
                }
            }
            return this;
        }

        // Loads and initializes all of the plug-ins and builds the DI container
        // but does not start their execution.
        public ILoadedContainer Build()
        {
            try
            {
                ConfigureLogging();

                LoadContainer();
                ComposeLoadedPlugins();
                SetKnownTypeDiscoveries();

                CreateAutofacContainer();
                CreateCompositeLogger();
            }
            catch (ContainerException ex)
            {
                LogException(ex);
                throw;
            }
            catch (Exception ex)
            {
                throw LogException(new ContainerException(
                    "unexpected container error", ex));
            }

            return this;
        }

        // The last step in the bootstrap process allowing plug-in modules to start  
        // runtime services (i.e. create queues and subscribe to events).
        public void Start()
        {
            if (_application.IsStarted)
            {
                throw LogException(new ContainerException(
                    "the application container plug-in modules have already been started"));
            }

            try
            {
                _application.StartPluginModules(_container);
                _logger.Verbose(() => Log.ToIndentedJson());
            }
            catch (ContainerException ex)
            {
                LogException(ex);
                throw;
            }
            catch (Exception ex)
            {
                throw LogException(new ContainerException(
                    "error starting container", ex));
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposing || _disposed) return;

            _logger?.Debug("container disposed");

            DisposePluginModules();
            this.Services?.Dispose();

            _disposed = true;
        }

        private void DisposePluginModules()
        {
            if (_application.AllPluginModules == null) return;

            foreach (var module in _application.AllPluginModules)
            {
                (module as IDisposable)?.Dispose();
            }
        }

        private Exception LogException(ContainerException ex)
        {
            if (_loggerConfig.LogExceptions)
            {
                _logger.Error($"{ex}");
            }
            return ex;
        }

        private static void ThrowIfDisposed(AppContainer container)
        {
            if (container._disposed)
            {
                throw new ContainerException(
                    "the application container has been disposed and can no longer be accessed");
            }
        }

        private static string[] AddDefaultSearchPatterns(string[] searchPatterns)
        {
            if (searchPatterns.ContainsAny(new[] { "*.dll", "NetFusion.*.dll", "NetFusion*.dll" }))
            {
                return searchPatterns;
            }

            return searchPatterns.Concat(new[] { "NetFusion.*.dll" }).ToArray();
        }

        // Determines if the host application specified how logging should
        // be configured.  If not specified, a Null Logger is used.
        private void ConfigureLogging()
        {
            _loggerConfig = _configs.Values.OfType<LoggerConfig>()
                .FirstOrDefault() ?? new LoggerConfig();

            _logger = _loggerConfig.Logger;
            if (_logger == null)
            {
                _logger = new NullLogger();
                _loggerConfig.LogExceptions = true;
            }

            Plugin.Log = _logger;
        }

        private void LoadContainer()
        {
            LoadManifestRegistry();
            LoadPlugins();
        }

        // Search all assemblies representing plug-ins.
        private void LoadManifestRegistry()
        {
            _typeResover.DiscoverManifests(this.Registry);

            AssertManifestProperties();
            AssertUniqueManifestIds();
            AssertLoadedManifests();
        }

        private void AssertManifestProperties()
        {
            var invalidManifestTypes = this.Registry.AllManifests
                .Where(m => m.PluginId.IsNullOrWhiteSpace())
                .Select(m => m.GetType());

            if (invalidManifestTypes.Any())
            {
                throw LogException(new ContainerException(
                    $"All manifest instances must have a PluginId specified.  " +
                    $"The following manifests are invalid: {String.Join(", ", invalidManifestTypes)}"));
            }

            invalidManifestTypes = this.Registry.AllManifests
                .Where(m => m.AssemblyName.IsNullOrWhiteSpace() || m.Name.IsNullOrWhiteSpace())
                .Select(m => m.GetType());

            if (invalidManifestTypes.Any())
            {
                throw LogException(new ContainerException(
                    $"All manifest instances must have Name and AssemblyName.  " +
                    $"The following manifests are invalid: {String.Join(", ", invalidManifestTypes)}"));
            }
        }

        private void AssertUniqueManifestIds()
        {
            var invalidManifestTypes = this.Registry.AllManifests
                .GroupBy(m => m.PluginId)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key);

            if (invalidManifestTypes.Any())
            {
                throw LogException(new ContainerException(
                    $"Plug-in identity values must be unique.  The following manifest PluginId " +
                    $"values are duplicated: {String.Join(", ", invalidManifestTypes)}"));
            }
        }

        private void AssertLoadedManifests()
        {
            if (this.Registry.AppHostPluginManifests.Empty())
            {
                throw LogException(new ContainerException(
                    $"An application plug-in manifest could not be found " +
                    $"derived from: {typeof(IAppHostPluginManifest)}"));
            }

            if (!this.Registry.AppHostPluginManifests.IsSingletonSet())
            {
                var ex = new ContainerException(
                    "More than one application plug-in manifest was found.",
                    this.Registry.AppHostPluginManifests.Select(am => new
                    {
                        ManifestType = am.GetType().FullName,
                        am.AssemblyName,
                        PluginName = am.Name,
                        am.PluginId
                    }));

                throw LogException(ex);
            }
        }

        // For each found plug-in manifest assembly, create a plug-in instance
        // associated with the manifest and add to composite application.
        private void LoadPlugins()
        {
            _application.Plugins = this.Registry.AllManifests
                .Select(m => new Plugin(m))
                .ToArray();

            _application.Plugins.ForEach(LoadPlugin);
        }
        
        private void LoadPlugin(Plugin plugin)
        {
            _typeResover.LoadPluginTypes(plugin);
            _typeResover.DiscoverModules(plugin);

            plugin.PluginConfigs = plugin.CreatedFrom(_configs.Values).ToList();
        }

        // This allows the plug-in to find concrete types deriving from IKnownPluginType.
        // This is how plug-in modules are composed.
        private void ComposeLoadedPlugins()
        {
            ComposeCorePlugins();
            ComposeAppPlugins();
        }

        // Core plug-in modules discover search for known-types contained within all plug-ins.
        private void ComposeCorePlugins()
        {
            var allPluginTypes = _application.GetPluginTypesFrom();

            _application.CorePlugins.ForEach(p =>
                ComposePluginModules(p, allPluginTypes));
        }

        // Application plug-in modules search for known-types contained only within other 
        // application plug-ins.  Core plug in types are not included since application plug-ins
        // never provide functionality to lower level plug-ins.
        private void ComposeAppPlugins()
        {
            var allAppPluginTypes = _application.GetPluginTypesFrom(PluginTypes.AppComponentPlugin, PluginTypes.AppHostPlugin);

            _application.AppComponentPlugins.ForEach(p =>
                ComposePluginModules(p, allAppPluginTypes));

            ComposePluginModules(_application.AppHostPlugin, allAppPluginTypes);
        }

        private void ComposePluginModules(Plugin plugin, IEnumerable<PluginType> fromPluginTypes)
        {
            var pluginDiscoveredTypes = new HashSet<Type>();
            foreach (var module in plugin.PluginModules)
            {
                var discoveredTypes = _typeResover.DiscoverKnownTypes(module, fromPluginTypes);
                discoveredTypes.ForEach(dt => pluginDiscoveredTypes.Add(dt));
            }

            plugin.SearchedForKnowTypes = pluginDiscoveredTypes.ToArray();
        }

        private void SetKnownTypeDiscoveries()
        {
            _application.Plugins.ForEach(SetDiscoveredKnowTypes);
        }

        // For plug-in derived known-type, find the plug-in(s) that discovered the type.  
        // This information is used for logging how the application was bootstrapped.
        private void SetDiscoveredKnowTypes(Plugin plugin)
        {
            foreach (PluginType pluginType in plugin.PluginTypes.Where(pt => pt.IsKnownType))
            {
                pluginType.DiscoveredByPlugins = _application.Plugins
                    .Where(p => p.SearchedForKnowTypes.Any(st => pluginType.Type.IsDerivedFrom(st)))
                    .ToArray();
            }
        }

        private void CreateAutofacContainer()
        {
            var builder = new Autofac.ContainerBuilder();

            // Allow the composite application plug-ins
            // to register services with container.
            _application.RegisterComponents(builder);

            // Register additional services,
            RegisterAppContainerAsService(builder);
            RegisterPluginModuleServices(builder);
            RegisterHostProvidedServices(builder);

            _container = builder.Build();
        }

        private void RegisterAppContainerAsService(Autofac.ContainerBuilder builder)
        {
            builder.RegisterInstance(this)
                .As<IAppContainer>()
                .SingleInstance();
        }

        private void RegisterPluginModuleServices(Autofac.ContainerBuilder builder)
        {
            var modulesWithServices = _application.AllPluginModules.OfType<IPluginModuleService>();

            modulesWithServices.ForEach(m =>
                builder.RegisterInstance(m).AsImplementedInterfaces()
                .SingleInstance());
        }

        // Allow the host application to register any service types or instances created during 
        // the initialization of the application. (i.e logger instance).
        private void RegisterHostProvidedServices(Autofac.ContainerBuilder builder)
        {
            var regConfig = _configs.Values.OfType<AutofacRegistrationConfig>().FirstOrDefault();

            if (regConfig != null && regConfig.Build != null)
            {
                regConfig.Build(builder);
            }
        }

        private void CreateCompositeLogger()
        {
            _containerLog = new AppContainerLog(_logger, _application, _container.ComponentRegistry);
        }
    }   
}