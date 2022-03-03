/*
 * Copyright (c) Facebook, Inc. and its affiliates.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using Facebook.WitAi.Lib;
using UnityEngine;

namespace Facebook.WitAi.Data.Entities
{
    public abstract class WitEntityDataBase<T>
    {
        public WitResponseNode responseNode;
        public string id;
        public string name;
        public string role;

        public int start;
        public int end;

        public string type;

        public string body;
        public T value;

        public float confidence;

        public WitResponseArray entities;

        public WitEntityDataBase<T> FromEntityWitResponseNode(WitResponseNode node)
        {
            responseNode = node;
            id = node[WitEntity.Fields.ID];
            name = node[WitEntity.Fields.NAME];
            role = node[WitEntity.Fields.ROLE];
            start = node[WitEntity.Fields.START].AsInt;
            end = node[WitEntity.Fields.END].AsInt;
            type = node[WitEntity.Fields.TYPE];
            body = node[WitEntity.Fields.BODY];
            confidence = node[WitEntity.Fields.CONFIDENCE].AsFloat;
            value = OnParseValue(node);
            entities = node[WitEntity.Fields.ENTITIES].AsArray;
            return this;
        }

        protected abstract T OnParseValue(WitResponseNode node);

        public override string ToString()
        {
            return value.ToString();
        }
    }

    public class WitEntityData : WitEntityDataBase<string>
    {
        public WitEntityData() {}

        public WitEntityData(WitResponseNode node)
        {
            FromEntityWitResponseNode(node);
        }

        protected override string OnParseValue(WitResponseNode node)
        {
            return node[WitEntity.Fields.VALUE];
        }

        public static implicit operator string(WitEntityData data) => data.value;
        public static bool operator ==(WitEntityData data, string value) => data?.value == value;
        public static bool operator !=(WitEntityData data, string value) => !(data == value);
        public static bool operator ==(string value, WitEntityData data) => data?.value == value;
        public static bool operator !=(string value, WitEntityData data) => !(data == value);

        public override bool Equals(object obj)
        {
            if (obj is string s) return s == value;
            return base.Equals(obj);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }

    public class WitEntityFloatData : WitEntityDataBase<float>
    {
        public WitEntityFloatData() {}

        public WitEntityFloatData(WitResponseNode node)
        {
            FromEntityWitResponseNode(node);
        }
        protected override float OnParseValue(WitResponseNode node)
        {
            return node[WitEntity.Fields.VALUE].AsFloat;
        }

        public bool Approximately(float v) => Mathf.Approximately(value, v);
        public static implicit operator float(WitEntityFloatData data) => data.value;
        public static bool operator ==(WitEntityFloatData data, float value) => data?.value == value;
        public static bool operator !=(WitEntityFloatData data, float value) => !(data == value);
        public static bool operator ==(WitEntityFloatData data, int value) => data?.value == value;
        public static bool operator !=(WitEntityFloatData data, int value) => !(data == value);
        public static bool operator ==(float value, WitEntityFloatData data) => data?.value == value;
        public static bool operator !=(float value, WitEntityFloatData data) => !(data == value);
        public static bool operator ==(int value, WitEntityFloatData data) => data?.value == value;
        public static bool operator !=(int value, WitEntityFloatData data) => !(data == value);

        public override bool Equals(object obj)
        {
            if (obj is float f) return f == value;
            return base.Equals(obj);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }

    public class WitEntityIntData : WitEntityDataBase<int>
    {
        public WitEntityIntData() {}

        public WitEntityIntData(WitResponseNode node)
        {
            FromEntityWitResponseNode(node);
        }
        protected override int OnParseValue(WitResponseNode node)
        {
            return node[WitEntity.Fields.VALUE].AsInt;
        }

        public static implicit operator int(WitEntityIntData data) => data.value;
        public static bool operator ==(WitEntityIntData data, int value) => data?.value == value;
        public static bool operator !=(WitEntityIntData data, int value) => !(data == value);
        public static bool operator ==(int value, WitEntityIntData data) => data?.value == value;
        public static bool operator !=(int value, WitEntityIntData data) => !(data == value);

        public override bool Equals(object obj)
        {
            if (obj is int i) return i == value;
            return base.Equals(obj);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }
}
