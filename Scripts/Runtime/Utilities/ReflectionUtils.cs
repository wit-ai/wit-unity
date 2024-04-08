/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Meta.WitAi.Utilities
{
    /// <summary>
    /// A utility class for performing reflection operations.
    /// It provides methods to reflect the value of a field, property, or method from an object or a serialized property.
    /// It also caches the fields, properties, and methods to avoid repeated reflection.
    /// </summary>
    public class ReflectionUtils
    {
        // Caches for fields, properties, and methods to avoid repeated reflection
        private static Dictionary<string, FieldInfo> _cachedFields = new Dictionary<string, FieldInfo>();
        private static Dictionary<string, PropertyInfo> _cachedProperties = new Dictionary<string, PropertyInfo>();
        private static Dictionary<string, MethodInfo> _cachedMethods = new Dictionary<string, MethodInfo>();

        /// <summary>
        /// Gets a cached field from a type. If the field is not in the cache, it is added.
        /// </summary>
        /// <param name="type">The type to get the field from.</param>
        /// <param name="fieldName">The name of the field.</param>
        /// <returns>The field info of the field.</returns>
        private static FieldInfo GetCachedField(Type type, string fieldName)
        {
            var field = $"{type.FullName}.{fieldName}";
            if (!_cachedFields.TryGetValue(field, out var fieldInfo))
            {
                fieldInfo = type.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
                _cachedFields[field] = fieldInfo;
            }

            return fieldInfo;
        }

        /// <summary>
        /// Gets a cached property from a type. If the property is not in the cache, it is added.
        /// </summary>
        /// <param name="type">The type to get the property from.</param>
        /// <param name="propertyName">The name of the property.</param>
        /// <returns>The property info of the property.</returns>
        private static PropertyInfo GetCachedProperty(Type type, string propertyName)
        {
            var property = $"{type.FullName}.{propertyName}";
            if (!_cachedProperties.TryGetValue(property, out var propertyInfo))
            {
                propertyInfo = type.GetProperty(propertyName, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
                _cachedProperties[property] = propertyInfo;
            }

            return propertyInfo;
        }

        /// <summary>
        /// Gets a cached method from a type. If the method is not in the cache, it is added.
        /// </summary>
        /// <param name="type">The type to get the method from.</param>
        /// <param name="methodName">The name of the method.</param>
        /// <returns>The method info of the method.</returns>
        private static MethodInfo GetCachedMethod(Type type, string methodName)
        {
            var method = $"{type.FullName}.{methodName}";
            if (!_cachedMethods.TryGetValue(method, out var methodInfo))
            {
                methodInfo = type.GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
                _cachedMethods[method] = methodInfo;
            }

            return methodInfo;
        }

        /// <summary>
        /// Reflects the value of a field from an object.
        /// </summary>
        /// <param name="obj">The object to reflect the field value from.</param>
        /// <param name="fieldName">The name of the field.</param>
        /// <param name="data">The output parameter to store the reflected field value.</param>
        /// <returns>True if the field exists, false otherwise.</returns>
        public static bool ReflectFieldValue<T>(object obj, string fieldName, out T data)
        {
            var fieldInfo = GetCachedField(obj.GetType(), fieldName);
            if (null != fieldInfo) data = (T) fieldInfo.GetValue(obj);
            else data = default(T);
            return null != fieldInfo;
        }

        /// <summary>
        /// Reflects the value of a property from an object.
        /// </summary>
        /// <param name="obj">The object to reflect the property value from.</param>
        /// <param name="fieldName">The name of the property.</param>
        /// <param name="data">The output parameter to store the reflected property value.</param>
        /// <returns>True if the property exists, false otherwise.</returns>
        public static bool ReflectPropertyValue<T>(object obj, string fieldName, out T data)
        {
            var fieldInfo = GetCachedProperty(obj.GetType(), fieldName);
            if (null != fieldInfo) data = (T) fieldInfo.GetValue(obj);
            else data = default(T);
            return null != fieldInfo;
        }

        /// <summary>
        /// Reflects the value of a method from an object.
        /// </summary>
        /// <param name="obj">The object to reflect the method value from.</param>
        /// <param name="fieldName">The name of the method.</param>
        /// <param name="data">The output parameter to store the reflected method value.</param>
        /// <returns>True if the method exists, false otherwise.</returns>
        public static bool ReflectMethodValue<T>(object obj, string fieldName, out T data)
        {
            var methodInfo = GetCachedMethod(obj.GetType(), fieldName);
            if (null != methodInfo) data = (T) methodInfo.Invoke(obj, null);
            else data = default(T);
            return null != methodInfo;
        }

        /// <summary>
        /// Get the value of a field, property, or method that is passed in by name from an object.
        ///
        /// Priority Search Order:
        /// 1. Field
        /// 2. Property
        /// 3. Method
        /// </summary>
        /// <param name="obj">The object to reflect the value from.</param>
        /// <param name="fieldName">The name of the field, property, or method.</param>
        /// <param name="value">The resulting reflected value</param>
        public static bool TryReflectValue<T>(object obj, string fieldName, out T value)
        {
            return ReflectFieldValue(obj, fieldName, out value) ||
                   ReflectPropertyValue(obj, fieldName, out value) ||
                   ReflectMethodValue(obj, fieldName, out value);
        }

        /// <summary>
        /// Get the value of a field, property, or method that is passed in by name from an object.
        ///
        /// Priority Search Order:
        /// 1. Field
        /// 2. Property
        /// 3. Method
        /// </summary>
        /// <param name="obj">The object to reflect the value from.</param>
        /// <param name="fieldName">The name of the field, property, or method.</param>
        /// <param name="value">The reflected value of the field/property/method called fieldName</param>
        /// <exception cref="ArgumentException">Thrown when no field, property, or method is found with the given name.</exception>
        public static T ReflectValue<T>(object obj, string fieldName)
        {
            if (TryReflectValue(obj, fieldName, out T data)) return data;

            // If no field, property, or method was found, throw an exception
            throw new ArgumentException($"No field, property, or method named '{fieldName}' was found.");
        }

        #if UNITY_EDITOR
        /// <summary>
        /// Get the value of a field, property, or method that is passed in by name from a serialized property.
        ///
        /// Priority Search Order:
        /// 1. Field
        /// 2. Property
        /// 3. Method
        /// </summary>
        /// <param name="obj">The object to reflect the value from.</param>
        /// <param name="fieldName">The name of the field, property, or method.</param>
        /// <returns>The reflected value.</returns>
        /// <exception cref="ArgumentException">Thrown when no field, property, or method is found with the given name.</exception>
        public static T ReflectPropertyValue<T>(SerializedProperty property, string fieldName)
        {
            return ReflectValue<T>(property.serializedObject.targetObject, fieldName);
        }

        /// <summary>
        /// Get the value of a field, property, or method that is passed in by name from a serialized property.
        ///
        /// Priority Search Order:
        /// 1. Field
        /// 2. Property
        /// 3. Method
        /// </summary>
        /// <param name="property">The SerializedProperty to reflect the value from.</param>
        /// <param name="fieldName">The name of the field, property, or method.</param>
        /// <param name="value">The reflected value of the field/property/method called fieldName</param>
        /// <returns>The reflected value.</returns>
        /// <exception cref="ArgumentException">Thrown when no field, property, or method is found with the given name.</exception>
        public static bool TryReflectPropertyValue<T>(SerializedProperty property, string fieldName, out T value)
        {
            return TryReflectValue<T>(property.serializedObject.targetObject, fieldName, out value);
        }
        #endif

        #region ITERATION
        /// <summary>
        /// Namespace prefix requirement for type lookups
        /// </summary>
        private const string NAMESPACE_PREFIX = "Meta";

        /// <summary>
        /// Check namespace prior to access
        /// </summary>
        private static bool IsValidNamespace(Type type) =>
            type?.Namespace != null && type.Namespace.StartsWith(NAMESPACE_PREFIX);

        /// <summary>
        /// Hefty editor method that iterates all assemblies
        /// </summary>
        private static IEnumerable<Type> GetTypes()
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(assembly =>
                {
                    try
                    {
                        return assembly.GetTypes();
                    }
                    catch
                    {
                        return new Type[] { };
                    }
                })
                .Where(IsValidNamespace);
        }

        /// <summary>
        /// Obtains methods with a specific valid callback
        /// </summary>
        private static IEnumerable<MethodInfo> GetMethods()
        {
            return GetTypes().SelectMany(type => type.GetMethods());
        }

        /// <summary>
        /// Retrieves all instantiatable types which are assignable from the given type T
        /// </summary>
        /// <param name="instance">the type on which this is called</param>
        /// <returns>a collection of types</returns>
        internal static Type[] GetAllAssignableTypes<T>() =>
            GetTypes().Where(type => typeof(T).IsAssignableFrom(type) && !type.IsInterface && !type.IsAbstract).ToArray();

        /// <summary>
        /// Retrieves all classes which are tagged with the specified T attribute
        /// </summary>
        internal static Type[] GetTypesWithAttribute<T>() where T : Attribute =>
            GetTypes().Where(type => type.GetCustomAttributes(typeof(T), false).Length > 0).ToArray();

        /// <summary>
        /// Retrieves all methods which are tagged with the specified T attribute
        /// </summary>
        internal static MethodInfo[] GetMethodsWithAttribute<T>() where T : Attribute =>
            GetMethods().Where(method => method.GetCustomAttributes(typeof(T), false).Length > 0).ToArray();
        #endregion ITERATION
    }
}
