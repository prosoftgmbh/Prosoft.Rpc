using System;

namespace Prosoft.Rpc
{
    public static class Client
    {
        public static Guid SessionId
        {
            get
            {
                return GetSession();
            }
        }

        public static Func<Guid> GetSession { get; set; }

        public static Func<Type, string> GetUri { get; set; }

        public static T Create<T>()
        {
            Type contractType = typeof(T);

            if (!contractType.IsInterface) throw new ArgumentException("Generic must be of type interface");

            var proxyType = ProxyObject.GetProxyObjectType(contractType);

            // Try to resolve uri for service type
            var uri = GetUri(contractType);

            if (uri == null) throw new InvalidOperationException(string.Format("Unable to resolve service uri for type '{0}'", contractType.FullName));

            return (T)Activator.CreateInstance(proxyType, contractType, uri, SessionId);
        }
    }
}