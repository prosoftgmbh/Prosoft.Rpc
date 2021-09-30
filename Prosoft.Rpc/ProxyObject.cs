using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;

namespace Prosoft.Rpc
{
    public abstract class ProxyObject
    {
        readonly Type _contractType;
        readonly string _uri;
        readonly Guid _sessionId;

        public ProxyObject(object contractType, object uri, object sessionId)
        {
            _contractType = (Type)contractType;
            _uri = ((string)uri).TrimEnd('/');
            _sessionId = (Guid)sessionId;
        }

        public static int Timeout { get; set; } = 300000;

        protected object Invoke(string methodName, params object[] parameters)
        {
            var method = _contractType.GetMethod(methodName);
            var requestUri = $"{_uri}?name={_contractType.FullName}&method={methodName}";
            byte[] contentData = null;

            if (parameters != null)
            {
                contentData = Utf8Json.JsonSerializer.Serialize(parameters);
            }

            var connTry = 0;
            HttpWebResponse response;

            do
            {
                response = null;

                var request = WebRequest.CreateHttp(requestUri);
                request.Method = "POST";
                request.Timeout = Timeout;

                if (_sessionId != Guid.Empty)
                {
                    request.Headers.Add("Cookie", "sessionId=" + _sessionId);
                }

                if (contentData != null)
                {
                    request.ContentType = "application/json";
                    request.ContentLength = contentData.Length;

                    using (var requestStream = request.GetRequestStream())
                    {
                        requestStream.Write(contentData, 0, contentData.Length);
                    }
                }
                else
                {
                    request.ContentLength = 0;
                }

                try
                {
                    response = (HttpWebResponse)request.GetResponse();

                    break;
                }
                catch (WebException ex)
                {
                    if (ex.Status == WebExceptionStatus.ProtocolError)
                    {
                        response = (HttpWebResponse)ex.Response;

                        break;
                    }
                }
                System.Threading.Thread.Sleep(1000);
            } while (connTry++ < 3);

            if (response == null) throw new Exception("Unknown error");

            if (response.StatusCode == System.Net.HttpStatusCode.OK && (method.ReturnType == null || method.ReturnType == typeof(void)))
            {
                response.Dispose();

                return null;
            }

            byte[] responseData = null;

            using (var stream = response.GetResponseStream())
            {
                using (var ms = new MemoryStream())
                {
                    stream.CopyTo(ms);
                    responseData = ms.ToArray();
                }
            }

            if (response.StatusCode != System.Net.HttpStatusCode.OK)
            {
                response.Dispose();
                throw new Exception(Encoding.UTF8.GetString(responseData));
            }

            response.Dispose();

            if (response == null) return null;

            return Utf8Json.JsonSerializer.NonGeneric.Deserialize(method.ReturnType, responseData);
        }

        static readonly Dictionary<Type, Type> proxyTypeCache = new Dictionary<Type, Type>();

        static readonly AssemblyName assemblyName = new AssemblyName("Prosoft.Rpc.ProxyAssembly");
        static readonly AssemblyBuilder assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.RunAndCollect);
        static readonly ModuleBuilder moduleBuilder = assemblyBuilder.DefineDynamicModule(assemblyName.Name);

        public static Type GetProxyObjectType(Type serviceType)
        {
            if (!proxyTypeCache.TryGetValue(serviceType, out var proxyType))
            {
                var invokeMethod = typeof(ProxyObject).GetMethod("Invoke", BindingFlags.Instance | BindingFlags.NonPublic);

                var typeBuilder = moduleBuilder.DefineType(serviceType.Name + new Random().Next(100000, 999999) + "Proxy", TypeAttributes.Public, typeof(ProxyObject), new Type[] { serviceType });

                ConstructorBuilder constructorBuilder = typeBuilder.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, new[] { typeof(object), typeof(object), typeof(object) });

                ILGenerator ctorIlGen = constructorBuilder.GetILGenerator();
                ctorIlGen.Emit(OpCodes.Ldarg_0);
                ctorIlGen.Emit(OpCodes.Ldarg_1);
                ctorIlGen.Emit(OpCodes.Ldarg_2);
                ctorIlGen.Emit(OpCodes.Ldarg_3);
                ctorIlGen.Emit(OpCodes.Call, typeof(ProxyObject).GetConstructor(new[] { typeof(object), typeof(object), typeof(object) }));
                ctorIlGen.Emit(OpCodes.Ret);

                var methods = serviceType.GetMethods();

                for (int j = 0; j < methods.Length; j++)
                {
                    var methodInfo = methods[j];
                    var parameterTypes = new List<Type>();

                    var parameters = methodInfo.GetParameters();

                    for (int i = 0; i < parameters.Length; i++)
                    {
                        var parameter = parameters[i];
                        parameterTypes.Add(parameter.ParameterType);
                    }

                    var methodBuilder = typeBuilder.DefineMethod(methodInfo.Name, MethodAttributes.Public | MethodAttributes.Virtual, methodInfo.ReturnType, parameterTypes.ToArray());

                    var ilGenerator = methodBuilder.GetILGenerator();

                    ilGenerator.Emit(OpCodes.Nop);
                    // Parameter: object this
                    ilGenerator.Emit(OpCodes.Ldarg_0);
                    // Parameter: string method
                    ilGenerator.Emit(OpCodes.Ldstr, methodInfo.Name);

                    if (parameterTypes.Count == 0)
                    {
                        ilGenerator.Emit(OpCodes.Ldnull);
                    }
                    else
                    {
                        ilGenerator.Emit(OpCodes.Ldc_I4, parameterTypes.Count);
                        ilGenerator.Emit(OpCodes.Newarr, typeof(object));

                        for (int i = 0; i < parameterTypes.Count; i++)
                        {
                            ilGenerator.Emit(OpCodes.Dup);
                            ilGenerator.Emit(OpCodes.Ldc_I4, i);

                            ilGenerator.Emit(OpCodes.Ldarg, (short)i + 1);

                            if (parameterTypes[i].IsValueType)
                            {
                                ilGenerator.Emit(OpCodes.Box, parameterTypes[i]);
                            }

                            ilGenerator.Emit(OpCodes.Stelem_Ref);
                        }
                    }

                    ilGenerator.Emit(OpCodes.Call, invokeMethod);

                    if (methodInfo.ReturnType == typeof(void))
                    {
                        ilGenerator.Emit(OpCodes.Pop);
                    }
                    else if (methodInfo.ReturnType.IsValueType)
                    {
                        ilGenerator.Emit(OpCodes.Unbox_Any, methodInfo.ReturnType);
                    }

                    ilGenerator.Emit(OpCodes.Ret);
                }

                proxyTypeCache[serviceType] = proxyType = typeBuilder.CreateTypeInfo().AsType();
            }

            return proxyType;
        }
    }
}