using Newtonsoft.Json;
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

        protected object Invoke(string methodName, params object[] parameters)
        {
            var method = _contractType.GetMethod(methodName);
            var requestUri = $"{_uri}?name={_contractType.FullName}&method={methodName}";
            byte[] contentData = null;

            if (parameters != null)
            {
                contentData = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(parameters));
            }

            var connTry = 0;
            HttpWebResponse response;

            do
            {
                response = null;

                var request = WebRequest.CreateHttp(requestUri);
                request.Method = "POST";
                request.Timeout = 300000;

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

            if (response == null)
                throw new Exception("Unknown error");

            if (response.StatusCode == System.Net.HttpStatusCode.OK && (method.ReturnType == null || method.ReturnType == typeof(void)))
            {
                response.Dispose();

                return null;
            }

            string responseContent = null;

            using (var stream = response.GetResponseStream())
            {
                var reader = new StreamReader(stream);
                responseContent = reader.ReadToEnd();
            }

            if (response.StatusCode != System.Net.HttpStatusCode.OK)
            {
                response.Dispose();
                throw new Exception(responseContent);
            }

            response.Dispose();

            return JsonConvert.DeserializeObject(responseContent, method.ReturnType);
        }

        static Dictionary<Type, Type> proxyTypeCache = new Dictionary<Type, Type>();

        static AssemblyName assemblyName = new AssemblyName("SimpleRpc.ProxyAssembly");
        static AssemblyBuilder assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.RunAndCollect);
        static ModuleBuilder moduleBuilder = assemblyBuilder.DefineDynamicModule(assemblyName.Name);

        public static Type GetProxyObjectType(Type serviceType)
        {
            Type proxyType;

            if (!proxyTypeCache.TryGetValue(serviceType, out proxyType))
            {
                var invokeMethod = typeof(ProxyObject).GetMethod("Invoke", BindingFlags.Instance | BindingFlags.NonPublic);

                var tb = moduleBuilder.DefineType(serviceType.Name + new Random().Next(100000, 999999) + "Proxy", TypeAttributes.Public, typeof(ProxyObject), new Type[] { serviceType });

                ConstructorBuilder ctor = tb.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard,
                    new[] { typeof(object), typeof(object), typeof(object) });

                ILGenerator ctorIlGen = ctor.GetILGenerator();
                ctorIlGen.Emit(OpCodes.Ldarg_0);
                ctorIlGen.Emit(OpCodes.Ldarg_1);
                ctorIlGen.Emit(OpCodes.Ldarg_2);
                ctorIlGen.Emit(OpCodes.Ldarg_3);
                ctorIlGen.Emit(OpCodes.Call, typeof(ProxyObject).GetConstructor(new[] { typeof(object), typeof(object), typeof(object) }));
                ctorIlGen.Emit(OpCodes.Ret);

                MethodInfo[] methods = serviceType.GetMethods();

                for (int j = 0; j < methods.Length; j++)
                {
                    MethodInfo m = methods[j];
                    List<Type> parameterTypes = new List<Type>();

                    ParameterInfo[] parameters = m.GetParameters();

                    for (int i = 0; i < parameters.Length; i++)
                    {
                        ParameterInfo p = parameters[i];
                        parameterTypes.Add(p.ParameterType);
                    }

                    var mb = tb.DefineMethod(m.Name, MethodAttributes.Public | MethodAttributes.Virtual, m.ReturnType, parameterTypes.ToArray());

                    var ilgen = mb.GetILGenerator();

                    ilgen.Emit(OpCodes.Nop);
                    // Parameter: object this
                    ilgen.Emit(OpCodes.Ldarg_0);
                    // Parameter: string method
                    ilgen.Emit(OpCodes.Ldstr, m.Name);

                    if (parameterTypes.Count == 0)
                    {
                        ilgen.Emit(OpCodes.Ldnull);
                    }
                    else
                    {
                        ilgen.Emit(OpCodes.Ldc_I4, parameterTypes.Count);
                        ilgen.Emit(OpCodes.Newarr, typeof(object));

                        for (int i = 0; i < parameterTypes.Count; i++)
                        {
                            ilgen.Emit(OpCodes.Dup);
                            ilgen.Emit(OpCodes.Ldc_I4, i);

                            ilgen.Emit(OpCodes.Ldarg, (short)i + 1);

                            if (parameterTypes[i].IsValueType)
                            {
                                ilgen.Emit(OpCodes.Box, parameterTypes[i]);
                            }

                            ilgen.Emit(OpCodes.Stelem_Ref);
                        }
                    }

                    ilgen.Emit(OpCodes.Call, invokeMethod);

                    if (m.ReturnType == typeof(void))
                    {
                        ilgen.Emit(OpCodes.Pop);
                    }
                    else if (m.ReturnType.IsValueType)
                    {
                        ilgen.Emit(OpCodes.Unbox_Any, m.ReturnType);
                    }

                    ilgen.Emit(OpCodes.Ret);
                }

                proxyTypeCache[serviceType] = proxyType = tb.CreateTypeInfo().AsType();
            }

            return proxyType;
        }
    }
}
