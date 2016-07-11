﻿using NetFusion.Bootstrap.Testing;
using NetFusion.Tests.Core.Bootstrap.Mocks;

namespace NetFusion.Tests.Core.Bootstrap
{
    public static class BootstrapTestExtensions
    {
        public static void SetupHostApp(this TestTypeResolver resolver)
        {
            resolver.AddPlugin<MockAppHostPlugin>();
        }
    }
}
