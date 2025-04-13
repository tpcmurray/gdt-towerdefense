using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace CyberDefense
{
    public class GameServiceProvider : IServiceProvider
    {
        private Dictionary<Type, object> services = new Dictionary<Type, object>();

        public void AddService(Type type, object service)
        {
            services[type] = service;
        }

        public object GetService(Type type)
        {
            if (services.ContainsKey(type))
            {
                return services[type];
            }
            return null;
        }
    }
}