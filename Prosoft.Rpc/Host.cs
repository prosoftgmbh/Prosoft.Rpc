using System;
using System.IO;
using System.Collections.Generic;

namespace Prosoft.Rpc
{
    public static class Host
    {
        readonly static Dictionary<string, Type> knownContractTypes = new Dictionary<string, Type>(StringComparer.InvariantCultureIgnoreCase);

        readonly static Dictionary<Type, Type> knownServiceTypes = new Dictionary<Type, Type>();

        public static byte[] Invoke(object serviceInstance, string methodName, byte[] requestData)
        {
            var type = serviceInstance.GetType();

            var methodInfo = type.GetMethod(methodName);

            var methodParameters = methodInfo.GetParameters();

            var parameters = new object[methodParameters.Length];

            if (requestData == null)
            {
                if (parameters.Length != 0) throw new Exception("Parameter count mismatch.");
            }
            else
            {
                var payloadParameterCount = BitConverter.ToInt32(requestData, 0);

                if (payloadParameterCount != methodParameters.Length) throw new Exception("Parameter count mismatch");

                var currentOffset = 4;

                for (int i = 0; i < methodParameters.Length; i++)
                {
                    var currentParameterLength = BitConverter.ToInt32(requestData, currentOffset);
                    currentOffset += 4;

                    try
                    {
                        parameters[i] = Utf8Json.JsonSerializer.NonGeneric.Deserialize(methodParameters[i].ParameterType, requestData, currentOffset);
                    }
                    catch(Exception e)
                    {
                        throw new Exception("Parameter type mismatch", e);
                    }

                    currentOffset += currentParameterLength;
                }
            }

            var result = methodInfo.Invoke(serviceInstance, parameters);

            if (methodInfo.ReturnType == typeof(void)) return null;

            return Utf8Json.JsonSerializer.NonGeneric.Serialize(methodInfo.ReturnType, result);
        }

        public static bool TryCreateInstance(string typeName, out object instance)
        {
            if (typeName == null) throw new ArgumentNullException("typeName");

            var contractType = ResolveContract(typeName);

            if (contractType == null)
            {
                instance = null;
                return false;
            }

            var serviceType = FindService(contractType);

            if (serviceType == null)
            {
                instance = null;
                return false;
            }

            instance = Activator.CreateInstance(serviceType);

            return true;
        }

        public static Type FindService(Type contractType)
        {
            if (knownServiceTypes.TryGetValue(contractType, out var type)) return type;

            var assemblies = AppDomain.CurrentDomain.GetAssemblies();

            for (int i = 0; i < assemblies.Length; i++)
            {
                if (assemblies[i].GetType().FullName == "System.Reflection.Emit.InternalAssemblyBuilder") continue;

                var types = assemblies[i].GetExportedTypes();

                for (int j = 0; j < types.Length; j++)
                {
                    if (contractType.IsAssignableFrom(types[j]))
                    {
                        return knownServiceTypes[contractType] = types[j];
                    }
                }
            }

            return knownServiceTypes[contractType] = null;
        }

        public static Type ResolveContract(string typeName)
        {
            if (knownContractTypes.TryGetValue(typeName, out var type)) return type;

            var assemblies = AppDomain.CurrentDomain.GetAssemblies();

            for (int i = 0; i < assemblies.Length; i++)
            {
                if (assemblies[i].GetType().FullName == "System.Reflection.Emit.InternalAssemblyBuilder") continue;

                var types = assemblies[i].GetExportedTypes();

                for (int j = 0; j < types.Length; j++)
                {
                    if (string.Compare(types[j].FullName, typeName, true) == 0)
                    {
                        return knownContractTypes[typeName] = types[j];
                    }
                }
            }

            return knownContractTypes[typeName] = null;
        }
    }
}