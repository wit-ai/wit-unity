/* * * * *
 * A simple JSON Parser / builder
 * ------------------------------
 *
 * It mainly has been written as a simple JSON parser. It can build a JSON string
 * from the node-tree, or generate a node tree from any valid JSON string.
 *
 * Written by Bunny83
 * 2012-06-09
 *
 * Changelog now external. See Changelog.txt
 *
 * The MIT License (MIT)
 *
 * Copyright (c) 2012-2019 Markus Göbel (Bunny83)
 *
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 *
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
 *
 * * * * */
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Conduit
{
    public enum ConduitNodeType
    {
        Array = 1,
        Object = 2,
        String = 3,
        Number = 4,
        NullValue = 5,
        Boolean = 6,
        None = 7,
        Custom = 0xFF,
    }
    public enum JSONTextMode
    {
        Compact,
        Indent
    }

    public abstract partial class ConduitNode
    {
        #region Enumerators
        public struct Enumerator
        {
            private enum Type { None, Array, Object }
            private Type type;
            private Dictionary<string, ConduitNode>.Enumerator m_Object;
            private List<ConduitNode>.Enumerator m_Array;
            public bool IsValid { get { return type != Type.None; } }
            public Enumerator(List<ConduitNode>.Enumerator aArrayEnum)
            {
                type = Type.Array;
                m_Object = default(Dictionary<string, ConduitNode>.Enumerator);
                m_Array = aArrayEnum;
            }
            public Enumerator(Dictionary<string, ConduitNode>.Enumerator aDictEnum)
            {
                type = Type.Object;
                m_Object = aDictEnum;
                m_Array = default(List<ConduitNode>.Enumerator);
            }
            public KeyValuePair<string, ConduitNode> Current
            {
                get
                {
                    if (type == Type.Array)
                        return new KeyValuePair<string, ConduitNode>(string.Empty, m_Array.Current);
                    else if (type == Type.Object)
                        return m_Object.Current;
                    return new KeyValuePair<string, ConduitNode>(string.Empty, null);
                }
            }
            public bool MoveNext()
            {
                if (type == Type.Array)
                    return m_Array.MoveNext();
                else if (type == Type.Object)
                    return m_Object.MoveNext();
                return false;
            }
        }
        public struct ValueEnumerator
        {
            private Enumerator m_Enumerator;
            public ValueEnumerator(List<ConduitNode>.Enumerator aArrayEnum) : this(new Enumerator(aArrayEnum)) { }
            public ValueEnumerator(Dictionary<string, ConduitNode>.Enumerator aDictEnum) : this(new Enumerator(aDictEnum)) { }
            public ValueEnumerator(Enumerator aEnumerator) { m_Enumerator = aEnumerator; }
            public ConduitNode Current { get { return m_Enumerator.Current.Value; } }
            public bool MoveNext() { return m_Enumerator.MoveNext(); }
            public ValueEnumerator GetEnumerator() { return this; }
        }
        public struct KeyEnumerator
        {
            private Enumerator m_Enumerator;
            public KeyEnumerator(List<ConduitNode>.Enumerator aArrayEnum) : this(new Enumerator(aArrayEnum)) { }
            public KeyEnumerator(Dictionary<string, ConduitNode>.Enumerator aDictEnum) : this(new Enumerator(aDictEnum)) { }
            public KeyEnumerator(Enumerator aEnumerator) { m_Enumerator = aEnumerator; }
            public string Current { get { return m_Enumerator.Current.Key; } }
            public bool MoveNext() { return m_Enumerator.MoveNext(); }
            public KeyEnumerator GetEnumerator() { return this; }
        }

        public class LinqEnumerator : IEnumerator<KeyValuePair<string, ConduitNode>>, IEnumerable<KeyValuePair<string, ConduitNode>>
        {
            private ConduitNode m_Node;
            private Enumerator m_Enumerator;
            internal LinqEnumerator(ConduitNode aNode)
            {
                m_Node = aNode;
                if (m_Node != null)
                    m_Enumerator = m_Node.GetEnumerator();
            }
            public KeyValuePair<string, ConduitNode> Current { get { return m_Enumerator.Current; } }
            object IEnumerator.Current { get { return m_Enumerator.Current; } }
            public bool MoveNext() { return m_Enumerator.MoveNext(); }

            public void Dispose()
            {
                m_Node = null;
                m_Enumerator = new Enumerator();
            }

            public IEnumerator<KeyValuePair<string, ConduitNode>> GetEnumerator()
            {
                return new LinqEnumerator(m_Node);
            }

            public void Reset()
            {
                if (m_Node != null)
                    m_Enumerator = m_Node.GetEnumerator();
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return new LinqEnumerator(m_Node);
            }
        }

        #endregion Enumerators

        #region common interface

        public static bool forceASCII = false; // Use Unicode by default
        public static bool longAsString = false; // lazy creator creates a JSONString instead of JSONNumber
        public static bool allowLineComments = true; // allow "//"-style comments at the end of a line

        public abstract ConduitNodeType Tag { get; }

        public virtual ConduitNode this[int aIndex] { get { return null; } set { } }

        public virtual ConduitNode this[string aKey] { get { return null; } set { } }

        public virtual string Value { get { return ""; } set { } }

        public virtual int Count { get { return 0; } }

        public virtual bool IsNumber { get { return false; } }
        public virtual bool IsString { get { return false; } }
        public virtual bool IsBoolean { get { return false; } }
        public virtual bool IsNull { get { return false; } }
        public virtual bool IsArray { get { return false; } }
        public virtual bool IsObject { get { return false; } }

        public virtual bool Inline { get { return false; } set { } }

        public virtual void Add(string aKey, ConduitNode aItem)
        {
        }
        public virtual void Add(ConduitNode aItem)
        {
            Add("", aItem);
        }

        public virtual ConduitNode Remove(string aKey)
        {
            return null;
        }

        public virtual ConduitNode Remove(int aIndex)
        {
            return null;
        }

        public virtual ConduitNode Remove(ConduitNode aNode)
        {
            return aNode;
        }
        public virtual void Clear() { }

        public virtual ConduitNode Clone()
        {
            return null;
        }

        public virtual IEnumerable<ConduitNode> Children
        {
            get
            {
                yield break;
            }
        }

        public IEnumerable<ConduitNode> DeepChildren
        {
            get
            {
                foreach (var C in Children)
                    foreach (var D in C.DeepChildren)
                        yield return D;
            }
        }

        public virtual bool HasKey(string aKey)
        {
            return false;
        }

        public virtual ConduitNode GetValueOrDefault(string aKey, ConduitNode aDefault)
        {
            return aDefault;
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            WriteToStringBuilder(sb, 0, 0, JSONTextMode.Compact);
            return sb.ToString();
        }

        public virtual string ToString(int aIndent)
        {
            StringBuilder sb = new StringBuilder();
            WriteToStringBuilder(sb, 0, aIndent, JSONTextMode.Indent);
            return sb.ToString();
        }
        internal abstract void WriteToStringBuilder(StringBuilder aSB, int aIndent, int aIndentInc, JSONTextMode aMode);

        public abstract Enumerator GetEnumerator();
        public IEnumerable<KeyValuePair<string, ConduitNode>> Linq { get { return new LinqEnumerator(this); } }
        public KeyEnumerator Keys { get { return new KeyEnumerator(GetEnumerator()); } }
        public ValueEnumerator Values { get { return new ValueEnumerator(GetEnumerator()); } }

        #endregion common interface

        #region typecasting properties


        public virtual double AsDouble
        {
            get
            {
                double v = 0.0;
                if (double.TryParse(Value, NumberStyles.Float, CultureInfo.InvariantCulture, out v))
                    return v;
                return 0.0;
            }
            set
            {
                Value = value.ToString(CultureInfo.InvariantCulture);
            }
        }

        public virtual int AsInt
        {
            get { return (int)AsDouble; }
            set { AsDouble = value; }
        }

        public virtual float AsFloat
        {
            get { return (float)AsDouble; }
            set { AsDouble = value; }
        }

        public virtual bool AsBool
        {
            get
            {
                bool v = false;
                if (bool.TryParse(Value, out v))
                    return v;
                return !string.IsNullOrEmpty(Value);
            }
            set
            {
                Value = (value) ? "true" : "false";
            }
        }

        public virtual long AsLong
        {
            get
            {
                long val = 0;
                if (long.TryParse(Value, out val))
                    return val;
                return 0L;
            }
            set
            {
                Value = value.ToString();
            }
        }

        public virtual ulong AsULong
        {
            get
            {
                ulong val = 0;
                if (ulong.TryParse(Value, out val))
                    return val;
                return 0;
            }
            set
            {
                Value = value.ToString();
            }
        }

        public virtual ConduitArray AsArray
        {
            get
            {
                return this as ConduitArray;
            }
        }

        public virtual ConduitObject AsObject
        {
            get
            {
                return this as ConduitObject;
            }
        }


        #endregion typecasting properties

        #region operators

        public static implicit operator ConduitNode(string s)
        {
            return (s == null) ? (ConduitNode) ConduitNull.CreateOrGet() : new ConduitString(s);
        }
        public static implicit operator string(ConduitNode d)
        {
            return (d == null) ? null : d.Value;
        }

        public static implicit operator ConduitNode(double n)
        {
            return new ConduitNumber(n);
        }
        public static implicit operator double(ConduitNode d)
        {
            return (d == null) ? 0 : d.AsDouble;
        }

        public static implicit operator ConduitNode(float n)
        {
            return new ConduitNumber(n);
        }
        public static implicit operator float(ConduitNode d)
        {
            return (d == null) ? 0 : d.AsFloat;
        }

        public static implicit operator ConduitNode(int n)
        {
            return new ConduitNumber(n);
        }
        public static implicit operator int(ConduitNode d)
        {
            return (d == null) ? 0 : d.AsInt;
        }

        public static implicit operator ConduitNode(long n)
        {
            if (longAsString)
                return new ConduitString(n.ToString());
            return new ConduitNumber(n);
        }
        public static implicit operator long(ConduitNode d)
        {
            return (d == null) ? 0L : d.AsLong;
        }

        public static implicit operator ConduitNode(ulong n)
        {
            if (longAsString)
                return new ConduitString(n.ToString());
            return new ConduitNumber(n);
        }
        public static implicit operator ulong(ConduitNode d)
        {
            return (d == null) ? 0 : d.AsULong;
        }

        public static implicit operator ConduitNode(bool b)
        {
            return new ConduitBool(b);
        }
        public static implicit operator bool(ConduitNode d)
        {
            return (d == null) ? false : d.AsBool;
        }

        public static implicit operator ConduitNode(KeyValuePair<string, ConduitNode> aKeyValue)
        {
            return aKeyValue.Value;
        }

        public static bool operator ==(ConduitNode a, object b)
        {
            if (ReferenceEquals(a, b))
                return true;
            bool aIsNull = a is ConduitNull || ReferenceEquals(a, null) || a is ConduitLazyCreator;
            bool bIsNull = b is ConduitNull || ReferenceEquals(b, null) || b is ConduitLazyCreator;
            if (aIsNull && bIsNull)
                return true;
            return !aIsNull && a.Equals(b);
        }

        public static bool operator !=(ConduitNode a, object b)
        {
            return !(a == b);
        }

        public override bool Equals(object obj)
        {
            return ReferenceEquals(this, obj);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        #endregion operators

        [ThreadStatic]
        private static StringBuilder m_EscapeBuilder;
        internal static StringBuilder EscapeBuilder
        {
            get
            {
                if (m_EscapeBuilder == null)
                    m_EscapeBuilder = new StringBuilder();
                return m_EscapeBuilder;
            }
        }
        internal static string Escape(string aText)
        {
            var sb = EscapeBuilder;
            sb.Length = 0;
            if (sb.Capacity < aText.Length + aText.Length / 10)
                sb.Capacity = aText.Length + aText.Length / 10;
            foreach (char c in aText)
            {
                switch (c)
                {
                    case '\\':
                        sb.Append("\\\\");
                        break;
                    case '\"':
                        sb.Append("\\\"");
                        break;
                    case '\n':
                        sb.Append("\\n");
                        break;
                    case '\r':
                        sb.Append("\\r");
                        break;
                    case '\t':
                        sb.Append("\\t");
                        break;
                    case '\b':
                        sb.Append("\\b");
                        break;
                    case '\f':
                        sb.Append("\\f");
                        break;
                    default:
                        if (c < ' ' || (forceASCII && c > 127))
                        {
                            ushort val = c;
                            sb.Append("\\u").Append(val.ToString("X4"));
                        }
                        else
                            sb.Append(c);
                        break;
                }
            }
            string result = sb.ToString();
            sb.Length = 0;
            return result;
        }

        private static ConduitNode ParseElement(string token, bool quoted)
        {
            if (quoted)
                return token;
            if (token.Length <= 5)
            {
                string tmp = token.ToLower();
                if (tmp == "false" || tmp == "true")
                    return tmp == "true";
                if (tmp == "null")
                    return ConduitNull.CreateOrGet();
            }
            double val;
            if (double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out val))
                return val;
            else
                return token;
        }

        public static ConduitNode Parse(string aJSON)
        {
            Stack<ConduitNode> stack = new Stack<ConduitNode>();
            ConduitNode ctx = null;
            int i = 0;
            StringBuilder Token = new StringBuilder();
            string TokenName = "";
            bool QuoteMode = false;
            bool TokenIsQuoted = false;
            bool HasNewlineChar = false;
            while (i < aJSON.Length)
            {
                switch (aJSON[i])
                {
                    case '{':
                        if (QuoteMode)
                        {
                            Token.Append(aJSON[i]);
                            break;
                        }
                        stack.Push(new ConduitObject());
                        if (ctx != null)
                        {
                            ctx.Add(TokenName, stack.Peek());
                        }
                        TokenName = "";
                        Token.Length = 0;
                        ctx = stack.Peek();
                        HasNewlineChar = false;
                        break;

                    case '[':
                        if (QuoteMode)
                        {
                            Token.Append(aJSON[i]);
                            break;
                        }

                        stack.Push(new ConduitArray());
                        if (ctx != null)
                        {
                            ctx.Add(TokenName, stack.Peek());
                        }
                        TokenName = "";
                        Token.Length = 0;
                        ctx = stack.Peek();
                        HasNewlineChar = false;
                        break;

                    case '}':
                    case ']':
                        if (QuoteMode)
                        {

                            Token.Append(aJSON[i]);
                            break;
                        }
                        if (stack.Count == 0)
                            throw new Exception("JSON Parse: Too many closing brackets");

                        stack.Pop();
                        if (Token.Length > 0 || TokenIsQuoted)
                            ctx.Add(TokenName, ParseElement(Token.ToString(), TokenIsQuoted));
                        if (ctx != null)
                            ctx.Inline = !HasNewlineChar;
                        TokenIsQuoted = false;
                        TokenName = "";
                        Token.Length = 0;
                        if (stack.Count > 0)
                            ctx = stack.Peek();
                        break;

                    case ':':
                        if (QuoteMode)
                        {
                            Token.Append(aJSON[i]);
                            break;
                        }
                        TokenName = Token.ToString();
                        Token.Length = 0;
                        TokenIsQuoted = false;
                        break;

                    case '"':
                        QuoteMode ^= true;
                        TokenIsQuoted |= QuoteMode;
                        break;

                    case ',':
                        if (QuoteMode)
                        {
                            Token.Append(aJSON[i]);
                            break;
                        }
                        if (Token.Length > 0 || TokenIsQuoted)
                            ctx.Add(TokenName, ParseElement(Token.ToString(), TokenIsQuoted));
                        TokenIsQuoted = false;
                        TokenName = "";
                        Token.Length = 0;
                        TokenIsQuoted = false;
                        break;

                    case '\r':
                    case '\n':
                        HasNewlineChar = true;
                        break;

                    case ' ':
                    case '\t':
                        if (QuoteMode)
                            Token.Append(aJSON[i]);
                        break;

                    case '\\':
                        ++i;
                        if (QuoteMode)
                        {
                            char C = aJSON[i];
                            switch (C)
                            {
                                case 't':
                                    Token.Append('\t');
                                    break;
                                case 'r':
                                    Token.Append('\r');
                                    break;
                                case 'n':
                                    Token.Append('\n');
                                    break;
                                case 'b':
                                    Token.Append('\b');
                                    break;
                                case 'f':
                                    Token.Append('\f');
                                    break;
                                case 'u':
                                    {
                                        string s = aJSON.Substring(i + 1, 4);
                                        Token.Append((char)int.Parse(
                                            s,
                                            System.Globalization.NumberStyles.AllowHexSpecifier));
                                        i += 4;
                                        break;
                                    }
                                default:
                                    Token.Append(C);
                                    break;
                            }
                        }
                        break;
                    case '/':
                        if (allowLineComments && !QuoteMode && i + 1 < aJSON.Length && aJSON[i + 1] == '/')
                        {
                            while (++i < aJSON.Length && aJSON[i] != '\n' && aJSON[i] != '\r') ;
                            break;
                        }
                        Token.Append(aJSON[i]);
                        break;
                    case '\uFEFF': // remove / ignore BOM (Byte Order Mark)
                        break;

                    default:
                        Token.Append(aJSON[i]);
                        break;
                }
                ++i;
            }
            if (QuoteMode)
            {
                throw new Exception("JSON Parse: Quotation marks seems to be messed up.");
            }
            if (ctx == null)
                return ParseElement(Token.ToString(), TokenIsQuoted);
            return ctx;
        }

    }
    // End of JSONNode

    public partial class ConduitArray : ConduitNode
    {
        private List<ConduitNode> m_List = new List<ConduitNode>();
        private bool inline = false;
        public override bool Inline
        {
            get { return inline; }
            set { inline = value; }
        }

        public override ConduitNodeType Tag { get { return ConduitNodeType.Array; } }
        public override bool IsArray { get { return true; } }
        public override Enumerator GetEnumerator() { return new Enumerator(m_List.GetEnumerator()); }

        public override ConduitNode this[int aIndex]
        {
            get
            {
                if (aIndex < 0 || aIndex >= m_List.Count)
                    return new ConduitLazyCreator(this);
                return m_List[aIndex];
            }
            set
            {
                if (value == null)
                    value = ConduitNull.CreateOrGet();
                if (aIndex < 0 || aIndex >= m_List.Count)
                    m_List.Add(value);
                else
                    m_List[aIndex] = value;
            }
        }

        public override ConduitNode this[string aKey]
        {
            get { return new ConduitLazyCreator(this); }
            set
            {
                if (value == null)
                    value = ConduitNull.CreateOrGet();
                m_List.Add(value);
            }
        }

        public override int Count
        {
            get { return m_List.Count; }
        }

        public override void Add(string aKey, ConduitNode aItem)
        {
            if (aItem == null)
                aItem = ConduitNull.CreateOrGet();
            m_List.Add(aItem);
        }

        public override ConduitNode Remove(int aIndex)
        {
            if (aIndex < 0 || aIndex >= m_List.Count)
                return null;
            ConduitNode tmp = m_List[aIndex];
            m_List.RemoveAt(aIndex);
            return tmp;
        }

        public override ConduitNode Remove(ConduitNode aNode)
        {
            m_List.Remove(aNode);
            return aNode;
        }

        public override void Clear()
        {
            m_List.Clear();
        }

        public override ConduitNode Clone()
        {
            var node = new ConduitArray();
            node.m_List.Capacity = m_List.Capacity;
            foreach(var n in m_List)
            {
                if (n != null)
                    node.Add(n.Clone());
                else
                    node.Add(null);
            }
            return node;
        }

        public override IEnumerable<ConduitNode> Children
        {
            get
            {
                foreach (ConduitNode N in m_List)
                    yield return N;
            }
        }


        internal override void WriteToStringBuilder(StringBuilder aSB, int aIndent, int aIndentInc, JSONTextMode aMode)
        {
            aSB.Append('[');
            int count = m_List.Count;
            if (inline)
                aMode = JSONTextMode.Compact;
            for (int i = 0; i < count; i++)
            {
                if (i > 0)
                    aSB.Append(',');
                if (aMode == JSONTextMode.Indent)
                    aSB.AppendLine();

                if (aMode == JSONTextMode.Indent)
                    aSB.Append(' ', aIndent + aIndentInc);
                m_List[i].WriteToStringBuilder(aSB, aIndent + aIndentInc, aIndentInc, aMode);
            }
            if (aMode == JSONTextMode.Indent)
                aSB.AppendLine().Append(' ', aIndent);
            aSB.Append(']');
        }
    }
    // End of JSONArray

    public partial class ConduitObject : ConduitNode
    {
        private Dictionary<string, ConduitNode> m_Dict = new Dictionary<string, ConduitNode>();

        private bool inline = false;
        public override bool Inline
        {
            get { return inline; }
            set { inline = value; }
        }

        public override ConduitNodeType Tag { get { return ConduitNodeType.Object; } }
        public override bool IsObject { get { return true; } }

        public override Enumerator GetEnumerator() { return new Enumerator(m_Dict.GetEnumerator()); }


        public override ConduitNode this[string aKey]
        {
            get
            {
                if (m_Dict.ContainsKey(aKey))
                    return m_Dict[aKey];
                else
                    return new ConduitLazyCreator(this, aKey);
            }
            set
            {
                if (value == null)
                    value = ConduitNull.CreateOrGet();
                if (m_Dict.ContainsKey(aKey))
                    m_Dict[aKey] = value;
                else
                    m_Dict.Add(aKey, value);
            }
        }

        public override ConduitNode this[int aIndex]
        {
            get
            {
                if (aIndex < 0 || aIndex >= m_Dict.Count)
                    return null;
                return m_Dict.ElementAt(aIndex).Value;
            }
            set
            {
                if (value == null)
                    value = ConduitNull.CreateOrGet();
                if (aIndex < 0 || aIndex >= m_Dict.Count)
                    return;
                string key = m_Dict.ElementAt(aIndex).Key;
                m_Dict[key] = value;
            }
        }

        public override int Count
        {
            get { return m_Dict.Count; }
        }

        public override void Add(string aKey, ConduitNode aItem)
        {
            if (aItem == null)
                aItem = ConduitNull.CreateOrGet();

            if (aKey != null)
            {
                if (m_Dict.ContainsKey(aKey))
                    m_Dict[aKey] = aItem;
                else
                    m_Dict.Add(aKey, aItem);
            }
            else
                m_Dict.Add(Guid.NewGuid().ToString(), aItem);
        }

        public override ConduitNode Remove(string aKey)
        {
            if (!m_Dict.ContainsKey(aKey))
                return null;
            ConduitNode tmp = m_Dict[aKey];
            m_Dict.Remove(aKey);
            return tmp;
        }

        public override ConduitNode Remove(int aIndex)
        {
            if (aIndex < 0 || aIndex >= m_Dict.Count)
                return null;
            var item = m_Dict.ElementAt(aIndex);
            m_Dict.Remove(item.Key);
            return item.Value;
        }

        public override ConduitNode Remove(ConduitNode aNode)
        {
            try
            {
                var item = m_Dict.Where(k => k.Value == aNode).First();
                m_Dict.Remove(item.Key);
                return aNode;
            }
            catch
            {
                return null;
            }
        }

        public override void Clear()
        {
            m_Dict.Clear();
        }

        public override ConduitNode Clone()
        {
            var node = new ConduitObject();
            foreach (var n in m_Dict)
            {
                node.Add(n.Key, n.Value.Clone());
            }
            return node;
        }

        public override bool HasKey(string aKey)
        {
            return m_Dict.ContainsKey(aKey);
        }

        public override ConduitNode GetValueOrDefault(string aKey, ConduitNode aDefault)
        {
            ConduitNode res;
            if (m_Dict.TryGetValue(aKey, out res))
                return res;
            return aDefault;
        }

        public override IEnumerable<ConduitNode> Children
        {
            get
            {
                foreach (KeyValuePair<string, ConduitNode> N in m_Dict)
                    yield return N.Value;
            }
        }

        internal override void WriteToStringBuilder(StringBuilder aSB, int aIndent, int aIndentInc, JSONTextMode aMode)
        {
            aSB.Append('{');
            bool first = true;
            if (inline)
                aMode = JSONTextMode.Compact;
            foreach (var k in m_Dict)
            {
                if (!first)
                    aSB.Append(',');
                first = false;
                if (aMode == JSONTextMode.Indent)
                    aSB.AppendLine();
                if (aMode == JSONTextMode.Indent)
                    aSB.Append(' ', aIndent + aIndentInc);
                aSB.Append('\"').Append(Escape(k.Key)).Append('\"');
                if (aMode == JSONTextMode.Compact)
                    aSB.Append(':');
                else
                    aSB.Append(" : ");
                k.Value.WriteToStringBuilder(aSB, aIndent + aIndentInc, aIndentInc, aMode);
            }
            if (aMode == JSONTextMode.Indent)
                aSB.AppendLine().Append(' ', aIndent);
            aSB.Append('}');
        }

    }
    // End of JSONObject

    public partial class ConduitString : ConduitNode
    {
        private string m_Data;

        public override ConduitNodeType Tag { get { return ConduitNodeType.String; } }
        public override bool IsString { get { return true; } }

        public override Enumerator GetEnumerator() { return new Enumerator(); }


        public override string Value
        {
            get { return m_Data; }
            set
            {
                m_Data = value;
            }
        }

        public ConduitString(string aData)
        {
            m_Data = aData;
        }
        public override ConduitNode Clone()
        {
            return new ConduitString(m_Data);
        }

        internal override void WriteToStringBuilder(StringBuilder aSB, int aIndent, int aIndentInc, JSONTextMode aMode)
        {
            aSB.Append('\"').Append(Escape(m_Data)).Append('\"');
        }
        public override bool Equals(object obj)
        {
            if (base.Equals(obj))
                return true;
            string s = obj as string;
            if (s != null)
                return m_Data == s;
            ConduitString s2 = obj as ConduitString;
            if (s2 != null)
                return m_Data == s2.m_Data;
            return false;
        }
        public override int GetHashCode()
        {
            return m_Data.GetHashCode();
        }
        public override void Clear()
        {
            m_Data = "";
        }
    }
    // End of JSONString

    public partial class ConduitNumber : ConduitNode
    {
        private double m_Data;

        public override ConduitNodeType Tag { get { return ConduitNodeType.Number; } }
        public override bool IsNumber { get { return true; } }
        public override Enumerator GetEnumerator() { return new Enumerator(); }

        public override string Value
        {
            get { return m_Data.ToString(CultureInfo.InvariantCulture); }
            set
            {
                double v;
                if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out v))
                    m_Data = v;
            }
        }

        public override double AsDouble
        {
            get { return m_Data; }
            set { m_Data = value; }
        }
        public override long AsLong
        {
            get { return (long)m_Data; }
            set { m_Data = value; }
        }
        public override ulong AsULong
        {
            get { return (ulong)m_Data; }
            set { m_Data = value; }
        }

        public ConduitNumber(double aData)
        {
            m_Data = aData;
        }

        public ConduitNumber(string aData)
        {
            Value = aData;
        }

        public override ConduitNode Clone()
        {
            return new ConduitNumber(m_Data);
        }

        internal override void WriteToStringBuilder(StringBuilder aSB, int aIndent, int aIndentInc, JSONTextMode aMode)
        {
            aSB.Append(Value);
        }
        private static bool IsNumeric(object value)
        {
            return value is int || value is uint
                || value is float || value is double
                || value is decimal
                || value is long || value is ulong
                || value is short || value is ushort
                || value is sbyte || value is byte;
        }
        public override bool Equals(object obj)
        {
            if (obj == null)
                return false;
            if (base.Equals(obj))
                return true;
            ConduitNumber s2 = obj as ConduitNumber;
            if (s2 != null)
                return m_Data == s2.m_Data;
            if (IsNumeric(obj))
                return Convert.ToDouble(obj) == m_Data;
            return false;
        }
        public override int GetHashCode()
        {
            return m_Data.GetHashCode();
        }
        public override void Clear()
        {
            m_Data = 0;
        }
    }
    // End of JSONNumber

    public partial class ConduitBool : ConduitNode
    {
        private bool m_Data;

        public override ConduitNodeType Tag { get { return ConduitNodeType.Boolean; } }
        public override bool IsBoolean { get { return true; } }
        public override Enumerator GetEnumerator() { return new Enumerator(); }

        public override string Value
        {
            get { return m_Data.ToString(); }
            set
            {
                bool v;
                if (bool.TryParse(value, out v))
                    m_Data = v;
            }
        }
        public override bool AsBool
        {
            get { return m_Data; }
            set { m_Data = value; }
        }

        public ConduitBool(bool aData)
        {
            m_Data = aData;
        }

        public ConduitBool(string aData)
        {
            Value = aData;
        }

        public override ConduitNode Clone()
        {
            return new ConduitBool(m_Data);
        }

        internal override void WriteToStringBuilder(StringBuilder aSB, int aIndent, int aIndentInc, JSONTextMode aMode)
        {
            aSB.Append((m_Data) ? "true" : "false");
        }
        public override bool Equals(object obj)
        {
            if (obj == null)
                return false;
            if (obj is bool)
                return m_Data == (bool)obj;
            return false;
        }
        public override int GetHashCode()
        {
            return m_Data.GetHashCode();
        }
        public override void Clear()
        {
            m_Data = false;
        }
    }
    // End of JSONBool

    public partial class ConduitNull : ConduitNode
    {
        static ConduitNull m_StaticInstance = new ConduitNull();
        public static bool reuseSameInstance = true;
        public static ConduitNull CreateOrGet()
        {
            if (reuseSameInstance)
                return m_StaticInstance;
            return new ConduitNull();
        }
        private ConduitNull() { }

        public override ConduitNodeType Tag { get { return ConduitNodeType.NullValue; } }
        public override bool IsNull { get { return true; } }
        public override Enumerator GetEnumerator() { return new Enumerator(); }

        public override string Value
        {
            get { return "null"; }
            set { }
        }
        public override bool AsBool
        {
            get { return false; }
            set { }
        }

        public override ConduitNode Clone()
        {
            return CreateOrGet();
        }

        public override bool Equals(object obj)
        {
            if (object.ReferenceEquals(this, obj))
                return true;
            return (obj is ConduitNull);
        }
        public override int GetHashCode()
        {
            return 0;
        }

        internal override void WriteToStringBuilder(StringBuilder aSB, int aIndent, int aIndentInc, JSONTextMode aMode)
        {
            aSB.Append("null");
        }
    }
    // End of JSONNull

    internal partial class ConduitLazyCreator : ConduitNode
    {
        private ConduitNode m_Node = null;
        private string m_Key = null;
        public override ConduitNodeType Tag { get { return ConduitNodeType.None; } }
        public override Enumerator GetEnumerator() { return new Enumerator(); }

        public ConduitLazyCreator(ConduitNode aNode)
        {
            m_Node = aNode;
            m_Key = null;
        }

        public ConduitLazyCreator(ConduitNode aNode, string aKey)
        {
            m_Node = aNode;
            m_Key = aKey;
        }

        private T Set<T>(T aVal) where T : ConduitNode
        {
            if (m_Key == null)
                m_Node.Add(aVal);
            else
                m_Node.Add(m_Key, aVal);
            m_Node = null; // Be GC friendly.
            return aVal;
        }

        public override ConduitNode this[int aIndex]
        {
            get { return new ConduitLazyCreator(this); }
            set { Set(new ConduitArray()).Add(value); }
        }

        public override ConduitNode this[string aKey]
        {
            get { return new ConduitLazyCreator(this, aKey); }
            set { Set(new ConduitObject()).Add(aKey, value); }
        }

        public override void Add(ConduitNode aItem)
        {
            Set(new ConduitArray()).Add(aItem);
        }

        public override void Add(string aKey, ConduitNode aItem)
        {
            Set(new ConduitObject()).Add(aKey, aItem);
        }

        public static bool operator ==(ConduitLazyCreator a, object b)
        {
            if (b == null)
                return true;
            return System.Object.ReferenceEquals(a, b);
        }

        public static bool operator !=(ConduitLazyCreator a, object b)
        {
            return !(a == b);
        }

        public override bool Equals(object obj)
        {
            if (obj == null)
                return true;
            return System.Object.ReferenceEquals(this, obj);
        }

        public override int GetHashCode()
        {
            return 0;
        }

        public override int AsInt
        {
            get { Set(new ConduitNumber(0)); return 0; }
            set { Set(new ConduitNumber(value)); }
        }

        public override float AsFloat
        {
            get { Set(new ConduitNumber(0.0f)); return 0.0f; }
            set { Set(new ConduitNumber(value)); }
        }

        public override double AsDouble
        {
            get { Set(new ConduitNumber(0.0)); return 0.0; }
            set { Set(new ConduitNumber(value)); }
        }

        public override long AsLong
        {
            get
            {
                if (longAsString)
                    Set(new ConduitString("0"));
                else
                    Set(new ConduitNumber(0.0));
                return 0L;
            }
            set
            {
                if (longAsString)
                    Set(new ConduitString(value.ToString()));
                else
                    Set(new ConduitNumber(value));
            }
        }

        public override ulong AsULong
        {
            get
            {
                if (longAsString)
                    Set(new ConduitString("0"));
                else
                    Set(new ConduitNumber(0.0));
                return 0L;
            }
            set
            {
                if (longAsString)
                    Set(new ConduitString(value.ToString()));
                else
                    Set(new ConduitNumber(value));
            }
        }

        public override bool AsBool
        {
            get { Set(new ConduitBool(false)); return false; }
            set { Set(new ConduitBool(value)); }
        }

        public override ConduitArray AsArray
        {
            get { return Set(new ConduitArray()); }
        }

        public override ConduitObject AsObject
        {
            get { return Set(new ConduitObject()); }
        }
        internal override void WriteToStringBuilder(StringBuilder aSB, int aIndent, int aIndentInc, JSONTextMode aMode)
        {
            aSB.Append("null");
        }
    }
    // End of JSONLazyCreator

    public static class JSON
    {
        public static ConduitNode Parse(string aJSON)
        {
            return ConduitNode.Parse(aJSON);
        }
    }
}
