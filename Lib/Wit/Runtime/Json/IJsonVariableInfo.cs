/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using System.Reflection;
using System.Collections.Generic;

namespace Meta.WitAi.Json
{
        /// <summary>
        /// Interface for simplifying interaction with a property/field
        /// </summary>
        internal interface IJsonVariableInfo
        {
            /// <summary>
            /// The desired variable names when serializing/deserializing
            /// </summary>
            string[] GetSerializeNames();

            /// <summary>
            /// Whether or not this variable should be serialized
            /// </summary>
            bool GetShouldSerialize();

            /// <summary>
            /// Whether or not this variable should be deserialized
            /// </summary>
            bool GetShouldDeserialize();

            /// <summary>
            /// The property/field type
            /// </summary>
            Type GetVariableType();

            /// <summary>
            /// Obtains the value of this property/field on a specific object
            /// </summary>
            object GetValue(object obj);

            /// <summary>
            /// Sets the value of this property/field onto a specific object
            /// </summary>
            void SetValue(object obj, object newValue);
        }

        /// <summary>
        /// Abstract base class for property/field access
        /// </summary>
        internal abstract class BaseJsonVariableInfo<T> : IJsonVariableInfo
            where T : MemberInfo
        {
            /// <summary>
            /// Stored PropertyInfo/FieldInfo
            /// </summary>
            protected T _info;

            /// <summary>
            /// Constructor that takes in FieldInfo/PropertyInfo
            /// </summary>
            protected BaseJsonVariableInfo(T info)
            {
                _info = info;
            }

            /// <summary>
            /// Obtains name of property/field
            /// </summary>
            protected virtual string GetName() => _info.Name;

            /// <summary>
            /// Whether a specified attribute is defined on this property/field
            /// </summary>
            protected virtual bool IsDefined<TAttribute>() where TAttribute : Attribute => _info.IsDefined(typeof(TAttribute), false);

            /// <summary>
            /// Obtains all instances of a specified attribute on this property/field
            /// </summary>
            protected virtual IEnumerable<TAttribute> GetCustomAttributes<TAttribute>() where TAttribute : Attribute =>
                _info.GetCustomAttributes<TAttribute>(false);

            /// <summary>
            /// The desired variable name when serializing/deserializing using custom
            /// attributes & base name.
            /// </summary>
            public virtual string[] GetSerializeNames()
            {
                // If no property attribute is found, ignore
                if (!IsDefined<JsonPropertyAttribute>())
                {
                    return new string[] { GetName() };
                }
                // Iterate property attributes
                List<string> results = new List<string>();
                foreach (var propertyAttribute in GetCustomAttributes<JsonPropertyAttribute>())
                {
                    string newName = propertyAttribute.PropertyName;
                    if (string.IsNullOrEmpty(newName))
                    {
                        newName = GetName();
                    }
                    if (!results.Contains(newName))
                    {
                        results.Add(newName);
                    }
                }
                return results.ToArray();
            }

            /// <summary>
            /// Can serialize if getter is public, there is a JsonPropertyAttribute & no JsonIgnore attribute
            /// </summary>
            public virtual bool GetShouldSerialize()
            {
                // If JsonIgnore, do not serialize
                if (IsDefined<JsonIgnoreAttribute>() || IsDefined<NonSerializedAttribute>())
                {
                    return false;
                }
                // If no getter, do not serialize
                if (!HasGet())
                {
                    return false;
                }
                // If public or marked as JsonProperty, serialize/deserialize
                return IsGetPublic() || IsDefined<JsonPropertyAttribute>();
            }

            /// <summary>
            /// Whether or not the get method exists
            /// </summary>
            protected abstract bool HasGet();

            /// <summary>
            /// Whether or not the get method can be used for this property/field
            /// </summary>
            protected abstract bool IsGetPublic();

            /// <summary>
            /// Only deserialize if can get & set
            /// </summary>
            public virtual bool GetShouldDeserialize()
            {
                // If JsonIgnore, do not serialize
                if (IsDefined<JsonIgnoreAttribute>() || IsDefined<NonSerializedAttribute>())
                {
                    return false;
                }
                // If no setter, do not serialize
                if (!HasSet())
                {
                    return false;
                }
                // If public or marked as JsonProperty, serialize/deserialize
                return IsSetPublic() || IsDefined<JsonPropertyAttribute>();
            }

            /// <summary>
            /// Whether or not the setter exists
            /// </summary>
            protected abstract bool HasSet();

            /// <summary>
            /// Whether or not the set method can be used for this property/field
            /// </summary>
            protected abstract bool IsSetPublic();

            public abstract Type GetVariableType();

            public abstract object GetValue(object obj);

            public abstract void SetValue(object obj, object newValue);

        }

        /// <summary>
        /// Wrapper for FieldInfo access
        /// </summary>
        internal class JsonFieldInfo : BaseJsonVariableInfo<FieldInfo>
        {
            public JsonFieldInfo(FieldInfo info) : base(info){}

            public override Type GetVariableType() => _info.FieldType;

            protected override bool HasGet() => true;

            protected override bool IsGetPublic() => _info.IsPublic;

            public override object GetValue(object obj) => _info.GetValue(obj);

            protected override bool HasSet() => true;

            protected override bool IsSetPublic() => _info.IsPublic;

            public override void SetValue(object obj, object value) => _info.SetValue(obj, value);
        }

        /// <summary>
        /// Wrapper for PropertyInfo access
        /// </summary>
        internal class JsonPropertyInfo : BaseJsonVariableInfo<PropertyInfo>
        {
            public JsonPropertyInfo(PropertyInfo info) : base(info){}

            public override Type GetVariableType() => _info.PropertyType;

            protected override bool HasGet() => _info.GetMethod != null;

            protected override bool IsGetPublic() => _info.GetMethod.IsPublic;

            public override object GetValue(object obj) => _info.GetValue(obj);

            protected override bool HasSet() => _info.SetMethod != null;

            protected override bool IsSetPublic() => _info.SetMethod.IsPublic;

            public override void SetValue(object obj, object value) => _info.SetValue(obj, value);
        }
}
