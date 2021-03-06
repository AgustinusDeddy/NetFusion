﻿using Autofac;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NetFusion.Bootstrap.Configuration;
using NetFusion.Bootstrap.Exceptions;
using NetFusion.Bootstrap.Logging;
using NetFusion.Bootstrap.Plugins;
using NetFusion.Common;
using NetFusion.Common.Extensions.Collection;
using NetFusion.Common.Extensions.Reflection;
using System;
using System.Collections.Generic;
using System.Linq;

namespace NetFusion.Bootstrap.Container
{
    /// <summary>
    /// Responsible for bootstrapping an application composed from plug-in components.  
    /// After the application container is created, services registered by the plug-in 
    /// modules, can be accessed using the configured dependency injection container. 
    /// </summary>
    public class AppContainer : IAppContainer,
        IComposite,
        IBuiltContainer
    {
        // Singleton instance of created container.
        private static AppContainer _instance;
        private bool _disposed = false;

        private readonly ITypeResolver _typeResover;
        private readonly Dictionary<Type, IContainerConfig> _configs;

        // Logging:
        private LoggerConfig _loggerConfig;
        private ILoggerFactory _loggerFactory;
        private ILogger _logger;
        private CompositeLog _compositeLog;

        // Settings:
        private EnvironmentConfig _enviromentConfig;
        private IConfiguration _configuration;

        // Contains references to all the discovered plug-in manifests
        // used to bootstrap the container.
        private ManifestRegistry Registry { get; }

        private readonly CompositeApplication _application;
        private Autofac.IContainer _container;

        /// <summary>
        /// Creates an instance of the application container.  The static Create methods of AppContainer 
        /// are the suggested method for creating a new container.  This constructor is usually used for 
        /// creating an application container for testing purposes.
        /// </summary>
        /// <param name="typeResolver">The type resolver implementation used to determine the plug-in
        /// components and their types.</param>
        /// <param name="setGlobalReference">Determines if AppContainer.Instance should be set to a
        /// singleton instance of the created container.  Useful for unit testing.</param>
        public AppContainer(ITypeResolver typeResolver, bool setGlobalReference = true)
        {
            Check.NotNull(typeResolver, nameof(ITypeResolver), "type resolver must be specified");

            _application = new CompositeApplication();
            _configs = new Dictionary<Type, IContainerConfig>();

            _typeResover = typeResolver;

            if (setGlobalReference)
            {
                _instance = this;
            }
            
            this.Registry = new ManifestRegistry();
        }

        public CompositeApplication Application
        {
            get { return _application; }
        }

        // Log of the composite application structure showing how it was constructed.
        public IDictionary<string, object> Log
        {
            get
            {
                ThrowIfDisposed(this);
                return _compositeLog?.GetLog() ?? new Dictionary<string, object>();
            }
        }

        public static IAppContainer Instance
        {
            get
            {
                ThrowIfDisposed(_instance);
                return _instance;
            }
        }

        public ILoggerFactory LoggerFactory
        {
            get
            {
                ThrowIfDisposed(this);
                return _loggerFactory;
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

        private static void ThrowIfDisposed(AppContainer container)
        {
            if (container._disposed)
            {
                throw new ContainerException(
                    "The application container has been disposed and can no longer be accessed.");
            }
        }

        //------------------------------------------IComposite Methods------------------------------------------//

        IEnumerable<Plugin> IComposite.Plugins => _application.Plugins;

        public Plugin GetPluginForType(Type type)
        {
            Check.NotNull(type, nameof(type), "type must be specified");

            if (_application.Plugins == null)
            {
                return null;
            }

            return _application.Plugins.FirstOrDefault(p => p.PluginTypes.Any(pt => pt.Type == type));
        }

        public Plugin GetPluginForFullTypeName(string typeName)
        {
            Check.NotNullOrWhiteSpace(typeName, nameof(typeName));

            if (_application.Plugins == null)
            {
                return null;
            }

            return _application.Plugins.FirstOrDefault(p => p.PluginTypes.Any(pt => pt.Type.FullName == typeName));
        }

        //---------------------------------------Container Creation-------------------------------//

        /// <summary>
        /// Creates an application container using the types provided by the specified type resolver.
        /// </summary>
        /// <param name="typeResolver">Reference to a custom type resolver.</param>
        /// <returns>Configured application container.</returns>
        public static IAppContainer Create(ITypeResolver typeResolver)
        {
            Check.NotNull(typeResolver, nameof(ITypeResolver), "type resolver must be specified");

            if (_instance != null)
            {
                throw new ContainerException("Container has already been created.");
            }

            return new AppContainer(typeResolver);
        }

        public IAppContainer WithConfig(IContainerConfig config)
        {
            Check.NotNull(config, nameof(config), "configuration not specified");

            if (this.Registry.AllManifests != null)
            {
                throw new ContainerException("Container has already been built.");
            }

            var configType = config.GetType();
            if (_configs.ContainsKey(configType))
            {
                throw new ContainerException(
                    $"Existing configuration of type: {config.GetType()} is already configured.");
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

        //------------------------------------------Container Build and Life Cycle-------------------------------------//

        // Loads and initializes all of the plug-ins and builds the DI container
        // but does not start their execution.
        public IBuiltContainer Build()
        {
            ConfigureLogging();
            ConfigureEnvironment();
            LogContainerInitialization();

            try
            {
                using (var logger = _logger.LogTraceDuration(BootstrapLogEvents.BOOTSTRAP_BUILD, "Building Container"))
                {
                    LoadContainer();
                    ComposeLoadedPlugins();
                    SetKnownTypeDiscoveries();
                    LogPlugins(_application.Plugins);

                    CreateAutofacContainer();
                    CreateCompositeLogger();
                }
            }
            catch (ContainerException ex)
            {
                LogException(ex);
                throw;
            }
            catch (Exception ex)
            {
                throw LogException(new ContainerException(
                    "Unexpected container error.", ex));
            }

            return this;
        }

        // The last step in the bootstrap process allowing plug-in modules to start  
        // runtime services.
        public void Start()
        {
            if (_application.IsStarted)
            {
                throw LogException(new ContainerException(
                    "The application container plug-in modules have already been started."));
            }

            try
            {
                using (var logger = _logger.LogTraceDuration(BootstrapLogEvents.BOOTSTRAP_START, "Starting Container"))
                {
                    _application.StartPluginModules(_container);
                }
               
                if (_logger.IsEnabled(LogLevel.Trace))
                {
                    _logger.LogTraceDetails(BootstrapLogEvents.BOOTSTRAP_COMPOSITE_LOG, "Composite Log", Log);
                }
            }
            catch (ContainerException ex)
            {
                LogException(ex);
                throw;
            }
            catch (Exception ex)
            {
                throw LogException(new ContainerException(
                    "Error starting container.", ex));
            }
        }

        public void Stop()
        {
            if (!_application.IsStarted)
            {
                throw LogException(new ContainerException(
                    "The application container plug-in modules have not been started."));
            }

            try
            {
                using (var logger = _logger.LogTraceDuration(BootstrapLogEvents.BOOTSTRAP_STOP, "Stopping Container"))
                {
                    _application.StopPluginModules(_container);
                }
            }
            catch (ContainerException ex)
            {
                LogException(ex);
                throw;
            }
            catch (Exception ex)
            {
                throw LogException(new ContainerException(
                    "Error stopping container.", ex));
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

            if (_application.IsStarted)
            {
                Stop();
            }

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

        // Configure overall environment settings such as .NET configuration.
        private void ConfigureEnvironment()
        {
            _enviromentConfig = _configs.Values.OfType<EnvironmentConfig>()
                .FirstOrDefault() ?? new EnvironmentConfig();

            _configuration = _enviromentConfig.Configuration;
        }

        private void LoadContainer()
        {
            LoadManifestRegistry();
            LoadPlugins();
        }


        //------------------------------------------Plug-in Loading-------------------------------------//

        // Delegate to the type resolver to search all assemblies representing plug-ins.
        private void LoadManifestRegistry()
        {
            var validator = new ManifestValidation(this.Registry);

            _typeResover.Initialize(this.LoggerFactory);
            _typeResover.SetPluginManifests(this.Registry);
            validator.Validate();

            LogManifests(this.Registry);
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
            _typeResover.SetPluginTypes(plugin);
            _typeResover.SetPluginModules(plugin);

            // Assign all configurations that are instances of types defined within plug-in.
            plugin.PluginConfigs = plugin.CreatedFrom(_configs.Values).ToList();
        }

        // This allows the plug-in to find concrete types deriving from IKnownPluginType.
        // This is how plug-in modules are composed.  All plug-in properties that are of 
        // type: IEnumerable<T> where T is a derived IKnownPluginType will be set to 
        // instances of types deriving from T.
        private void ComposeLoadedPlugins()
        {
            ComposeCorePlugins();
            ComposeAppPlugins();
        }

        // Core plug-in modules discover derived known-types contained within *all* plug-ins.
        private void ComposeCorePlugins()
        {
            var allPluginTypes = _application.GetPluginTypes();

            _application.CorePlugins.ForEach(p =>
                ComposePluginModules(p, allPluginTypes));
        }

        // Application plug-in modules search for derived known-types contained *only* within other 
        // application plug-ins.  Core plug in types are not included since application plug-ins
        // never provide functionality to lower level plug-ins.
        private void ComposeAppPlugins()
        {
            var allAppPluginTypes = _application.GetPluginTypes(PluginTypes.AppComponentPlugin, PluginTypes.AppHostPlugin);

            _application.AppComponentPlugins.ForEach(p =>
                ComposePluginModules(p, allAppPluginTypes));

            ComposePluginModules(_application.AppHostPlugin, allAppPluginTypes);
        }

        private void ComposePluginModules(Plugin plugin, IEnumerable<PluginType> fromPluginTypes)
        {
            var pluginDiscoveredTypes = new HashSet<Type>();
            foreach (IPluginModule module in plugin.PluginModules)
            {
                IEnumerable<Type> discoveredTypes = _typeResover.SetPluginModuleKnownTypes(module, fromPluginTypes);
                discoveredTypes.ForEach(dt => pluginDiscoveredTypes.Add(dt));
            }

            plugin.DiscoveredTypes = pluginDiscoveredTypes.ToArray();
        }

        private void SetKnownTypeDiscoveries()
        {
            _application.Plugins.ForEach(SetDiscoveredKnowTypes);
        }

        // For plug-in derived known-type, find the plug-in(s) that discovered the type.  
        // This information is used for logging how the application was composed.
        private void SetDiscoveredKnowTypes(Plugin plugin)
        {
            foreach (PluginType knownType in plugin.PluginTypes.Where(pt => pt.IsKnownType))
            {
                knownType.DiscoveredByPlugins = _application.Plugins
                    .Where(p => p.DiscoveredTypes.Any(dt => knownType.Type.IsConcreteTypeDerivedFrom(dt)))
                    .ToArray();
            }
        }

        //------------------------------------------DI Container Build------------------------------------------//

        private void CreateAutofacContainer()
        {
            var builder = new Autofac.ContainerBuilder();

            // Allow the composite application plug-in modules
            // to register services with container.
            _application.RegisterComponents(builder);

            // Register additional services,
            RegisterAppContainerAsService(builder);
            RegisterPluginModuleServices(builder);
            RegisterHostProvidedServices(builder);

            // Register logging and configuration.
            RegisterLogging(builder);
            RegisterConfigSettings(builder);

            _container = builder.Build();
        }

        private void RegisterAppContainerAsService(Autofac.ContainerBuilder builder)
        {
            builder.RegisterInstance(this).As<IAppContainer>().SingleInstance();
        }

        private void RegisterLogging(Autofac.ContainerBuilder builder)
        {
            builder.RegisterInstance(this.LoggerFactory).As<ILoggerFactory>().SingleInstance();
            builder.RegisterGeneric(typeof(Logger<>)).As(typeof(ILogger<>)).SingleInstance();
        }

        private void RegisterConfigSettings(Autofac.ContainerBuilder builder)
        {
            builder.RegisterGeneric(typeof(OptionsManager<>)).As(typeof(IOptions<>)).SingleInstance();
            builder.RegisterGeneric(typeof(OptionsMonitor<>)).As(typeof(IOptionsMonitor<>)).SingleInstance();
            builder.RegisterGeneric(typeof(OptionsSnapshot<>)).As(typeof(IOptionsSnapshot<>)).InstancePerLifetimeScope();
            builder.RegisterInstance(_configuration).As<IConfiguration>();
        }

        private void RegisterPluginModuleServices(Autofac.ContainerBuilder builder)
        {
            var modulesWithServices = _application.AllPluginModules.OfType<IPluginModuleService>();

            foreach (IPluginModuleService moduleService in modulesWithServices)
            {
                var moduleServiceType = moduleService.GetType();
                var moduleServiceInterfaces = moduleServiceType.GetInterfacesDerivedFrom<IPluginModuleService>();

                builder.RegisterInstance(moduleService)
                    .As(moduleServiceInterfaces.ToArray())
                    .SingleInstance();
            }
        }

        // Allow the host application to register any service types or instances created during 
        // the initialization of the application.
        private void RegisterHostProvidedServices(Autofac.ContainerBuilder builder)
        {
            var regConfig = _configs.Values.OfType<AutofacRegistrationConfig>().FirstOrDefault();

            if (regConfig != null && regConfig.Build != null)
            {
                regConfig.Build(builder);
            }
        }

        //------------------------------------------Logging------------------------------------------//

        // Determines if the host application specified how logging should
        // be configured.  If not specified, a default configuration is used.
        private void ConfigureLogging()
        {
            _loggerConfig = _configs.Values.OfType<LoggerConfig>()
               .FirstOrDefault() ?? new LoggerConfig();

            _loggerFactory = _loggerConfig.LoggerFactory;

            if (_loggerFactory == null)
            {
                _loggerFactory = new LoggerFactory();
                _loggerFactory.AddDebug(LogLevel.Warning);
                _loggerConfig.LogExceptions = true;
            }

            _logger = _loggerFactory.CreateLogger<AppContainer>();
            _application.LoggerFactory = _loggerFactory;
        }

        private Exception LogException(Exception ex)
        {
            if (_loggerConfig.LogExceptions)
            {
                _logger.LogError(BootstrapLogEvents.BOOTSTRAP_EXCEPTION, "Bootstrap Exception", ex);
            }
            return ex;
        }

        private void CreateCompositeLogger()
        {
            _compositeLog = new CompositeLog(_application, _container.ComponentRegistry);
        }

        private void LogContainerInitialization()
        {
            _logger.LogDebugDetails(BootstrapLogEvents.BOOTSTRAP_INITIALIZE, "Container Setup", new
            {
                TypeResolver = _typeResover.GetType(),
                Configs = _configs.Keys.Select(ct => ct.AssemblyQualifiedName)
            });
        }

        private void LogManifests(ManifestRegistry registry)
        {
            _logger.LogDebugDetails(BootstrapLogEvents.BOOTSTRAP_EXCEPTION, "Manifests", new
            {
                Host = registry.AppHostPluginManifests.First().GetType(),
                Application = registry.AppComponentPluginManifests.Select(m => m.GetType()),
                Core = registry.CorePluginManifests.Select(c => c.GetType())
            });
        }

        private void LogPlugins(Plugin[] plugins)
        {
            foreach (var plugin in plugins)
            {
                _logger.LogDebugDetails(BootstrapLogEvents.BOOTSTRAP_PLUGIN_DETAILS, "Plugin", new
                {
                    plugin.Manifest.Name,
                    plugin.Manifest.PluginId,
                    plugin.AssemblyName,
                    Configs = plugin.PluginConfigs.Select(c => c.GetType().Name),
                    Modules = plugin.PluginModules.Select(m => m.GetType().Name),
                    Discovers = plugin.DiscoveredTypes.Select(t => t.Name)
                });
            }
        }
    }
}