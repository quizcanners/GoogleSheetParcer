using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json.Linq;
using PlayerAndEditorGUI;
using System;

namespace QuizCannersUtilities
{

#pragma warning disable IDE0018 // Inline variable declaration
#pragma warning disable IDE0019 // Use pattern matching

    public interface IJObjectCustom
    {
        void Decode(string key, JToken token);
    }

    public static class QcJsonParsing
    {


        public static void DecodeList_Indexed<T>(this QcGoogleSheetParcer parser, List<T> list, bool ignoreErrors = true) where T : IJObjectCustom, IGotIndex, new()
        {
            var token = parser.GetJToken();

            if (token != null)
            {
                var enm = DecodeCollection(token);

                foreach (var tok in enm)
                {
                    var el = new T();

                    if (ignoreErrors)
                    {
                        try
                        {
                            el.Decode(tok);

                            if (el.IndexForPEGI != -1)
                                list.AddOrReplaceByIGotIndex(el);
                        }
                        catch (Exception ex)
                        {
                            Debug.LogError("[{0}] ".F(el.GetNameForInspector())  + ex.ToString());
                        }
                    }
                    else
                    {
                        el.Decode(tok);

                        if (el.IndexForPEGI != -1)
                            list.AddOrReplaceByIGotIndex(el);
                    }

                }
            }
            else
                Debug.LogError("No Token");
        }

        public static bool IsNullOrEmpty(this JToken token)
        {
            if (token == null)
                return true;

            switch (token.Type)
            {
                case JTokenType.Array:
                case JTokenType.Object: return !token.HasValues;
                case JTokenType.String: return token.ToString() == string.Empty;
                case JTokenType.Null: return true;
                default: QcUnity.ChillLogger.LogErrorOnce("Unknown Token Type: {0}".F(token.Type), token.Type.ToString()); return false;
            }
        }

        public static int ToInt(this JToken token, int defaultValue = 0) => token.IsNullOrEmpty() ? defaultValue : (int)token;

        public static void ToEnum<T>(this JToken token, ref T myEnum) where T : struct
        {
            var val = token as JValue;

            if (val == null)
            {
                Debug.Log("{0} is not JValue");
                return;
            }

            T enumValue;

            if (Enum.TryParse(token.ToString(), out enumValue))
                myEnum = enumValue;
        }

        public static IEnumerable<JToken> DecodeCollection(JToken token)
        {
            var jArr = token as JArray;
            if (jArr == null)
            {
                Debug.LogError("{0} is not JObject".F(jArr.ToString()));
                yield break;
            }

            foreach (var el in jArr)
            {
                yield return el;
            }
        }

        public static void Decode(this IJObjectCustom obj, JToken token)
        {
            var jObj = token as JObject;
            if (jObj == null)
            {
                Debug.LogError("{0} is not JObject".F(token.ToString()));
                return; ;
            }

            foreach (var value in jObj)
            {
                obj.Decode(value.Key, value.Value);
            }
        }

  
    }

    public class JTokenInspector
    {
        private readonly TogglesTree toggles = new TogglesTree();

        private bool Inspect(JToken token, TogglesTree tree)
        {
            if (token is JArray)
            {
                var jArr = token.Value<JArray>();

                pegi.Indent();

                for (int i = 0; i < jArr.Count; i++)
                {
                    JToken current = jArr[i].Value<JToken>();

                    var open = tree.GetIsFoldedOut(i);

                    if ("[{0}]".F(i).foldout(ref open).nl())
                        Inspect(current, tree.GetOrCreateChildTree(i));

                    tree.SetIsFoldedOut(i, open);
                }

                pegi.UnIndent();

            }
            else if (token is JObject)
            {
                var jObj = token.Value<JObject>();

                int i = 0;

                pegi.Indent();

                foreach (var child in jObj)
                {
                    var open = tree.GetIsFoldedOut(i);

                    if ("[{0}] : {1}".F(i, child.Key).foldout(ref open).nl())
                        Inspect(child.Value, tree.GetOrCreateChildTree(i));

                    tree.SetIsFoldedOut(i, open);

                    i++;
                }

                pegi.UnIndent();
            }
            else if (token is JValue)
            {
                pegi.Indent();
                var jVal = token as JValue;
                jVal.ToString().nl();
                pegi.UnIndent();
            }
            else
            {
                pegi.Indent();
                token.GetType().ToPegiStringType().nl();
                pegi.UnIndent();
            }

            return false;
        }

        public bool Inspect(JToken token)
        {
            var changed = false;

            if (token == null)
                "Token is null".nl();
            else
                Inspect(token, toggles).nl(ref changed);

            return changed;
        }

        internal class TogglesTree
        {
            private Dictionary<int, TogglesTree> children;
            public bool IsTrue;

            public TogglesTree GetOrCreateChildTree(int childIndex)
            {
                if (children == null)
                    children = new Dictionary<int, TogglesTree>();

                TogglesTree child;

                if (!children.TryGetValue(childIndex, out child))
                {
                    child = new TogglesTree();
                    children[childIndex] = child;
                }

                return child;
            }

            public bool GetIsFoldedOut(int childIndex)
            {
                if (children == null)
                    return false;

                TogglesTree tree;

                if (!children.TryGetValue(childIndex, out tree))
                    return false;

                return tree.IsTrue;
            }

            public void SetIsFoldedOut(int childIndex, bool value)
            {
                var current = GetIsFoldedOut(childIndex);

                if (current == value)
                    return;

                TogglesTree childTree = GetOrCreateChildTree(childIndex);

                childTree.IsTrue = value;

                if (!value && childTree.AllFalse)
                    children.Remove(childIndex);
            }

            public bool AllFalse
            {
                get
                {
                    if (IsTrue)
                        return false;

                    if (children == null)
                        return true;

                    foreach (var foldout in children)
                        if (!foldout.Value.AllFalse)
                            return false;

                    return true;
                }
            }
        }
    }

    
}