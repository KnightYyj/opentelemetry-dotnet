// <copyright file="OpenTelemetryServicesExtensions.cs" company="OpenTelemetry Authors">
// Copyright The OpenTelemetry Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>

using System;
using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using OpenTelemetry;
using OpenTelemetry.Extensions.Hosting.Implementation;
using OpenTelemetry.Internal;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Microsoft.Extensions.DependencyInjection
{
    /// <summary>
    /// Extension methods for setting up OpenTelemetry services in an <see cref="IServiceCollection" />.
    /// </summary>
    public static class OpenTelemetryServicesExtensions
    {
        /// <summary>
        /// Configure OpenTelemetry and register a <see cref="IHostedService"/>
        /// to automatically start tracing services in the supplied <see
        /// cref="IServiceCollection" />.
        /// </summary>
        /// <remarks>
        /// Note: This is safe to be called multiple times and by library authors.
        /// Only a single <see cref="TracerProvider"/> will be created for a given
        /// <see cref="IServiceCollection"/>.
        /// </remarks>
        /// <param name="services"><see cref="IServiceCollection"/>.</param>
        /// <returns>Supplied <see cref="IServiceCollection"/> for chaining calls.</returns>
        public static IServiceCollection AddOpenTelemetryTracing(this IServiceCollection services)
            => AddOpenTelemetryTracing(services, (b) => { });

        /// <summary>
        /// Configure OpenTelemetry and register a <see cref="IHostedService"/>
        /// to automatically start tracing services in the supplied <see
        /// cref="IServiceCollection" />.
        /// </summary>
        /// <remarks><inheritdoc cref="AddOpenTelemetryTracing(IServiceCollection)" path="/remarks"/></remarks>
        /// <param name="services"><see cref="IServiceCollection"/>.</param>
        /// <param name="configure">Callback action to configure the <see cref="TracerProviderBuilder"/>.</param>
        /// <returns>Supplied <see cref="IServiceCollection"/> for chaining calls.</returns>
        public static IServiceCollection AddOpenTelemetryTracing(this IServiceCollection services, Action<TracerProviderBuilder> configure)
        {
            Guard.ThrowIfNull(services);

            services.ConfigureOpenTelemetryTracing(configure);

            services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, TelemetryHostedService>());

            return services;
        }

        /// <summary>
        /// Adds OpenTelemetry MeterProvider to the specified <see cref="IServiceCollection" />.
        /// </summary>
        /// <param name="services">The <see cref="IServiceCollection" /> to add services to.</param>
        /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
        public static IServiceCollection AddOpenTelemetryMetrics(this IServiceCollection services)
        {
            return services.AddOpenTelemetryMetrics(builder => { });
        }

        /// <summary>
        /// Adds OpenTelemetry MeterProvider to the specified <see cref="IServiceCollection" />.
        /// </summary>
        /// <param name="services">The <see cref="IServiceCollection" /> to add services to.</param>
        /// <param name="configure">Callback action to configure the <see cref="MeterProviderBuilder"/>.</param>
        /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
        public static IServiceCollection AddOpenTelemetryMetrics(this IServiceCollection services, Action<MeterProviderBuilder> configure)
        {
            Guard.ThrowIfNull(configure);

            var builder = new MeterProviderBuilderHosting(services);
            configure(builder);
            return services.AddOpenTelemetryMetrics(sp => builder.Build(sp));
        }

        /// <summary>
        /// Adds OpenTelemetry MeterProvider to the specified <see cref="IServiceCollection" />.
        /// </summary>
        /// <param name="services">The <see cref="IServiceCollection" /> to add services to.</param>
        /// <param name="createMeterProvider">A delegate that provides the tracer provider to be registered.</param>
        /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
        private static IServiceCollection AddOpenTelemetryMetrics(this IServiceCollection services, Func<IServiceProvider, MeterProvider> createMeterProvider)
        {
            Debug.Assert(services != null, $"{nameof(services)} must not be null");
            Debug.Assert(createMeterProvider != null, $"{nameof(createMeterProvider)} must not be null");

            // Accessing Sdk class is just to trigger its static ctor,
            // which sets default Propagators and default Activity Id format
            _ = Sdk.SuppressInstrumentation;

            try
            {
                services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, TelemetryHostedService>());
                return services.AddSingleton(s => createMeterProvider(s));
            }
            catch (Exception ex)
            {
                HostingExtensionsEventSource.Log.FailedInitialize(ex);
            }

            return services;
        }
    }
}
