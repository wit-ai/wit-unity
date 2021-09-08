/*
 * Copyright (c) Facebook, Inc. and its affiliates.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Net;
using com.facebook.witai.data;
using com.facebook.witai.events;
using com.facebook.witai.interfaces;
using com.facebook.witai.lib;
using UnityEngine.Serialization;


namespace com.facebook.witai
{

	public class WitSimpleEntityList : IEntityListProvider
    {
        public List<string> keywords;
        public string entity;

        public WitSimpleEntityList(string entityIdentifier, List<string> words)
        {
            entity = entityIdentifier;
            keywords = words;
        }

        public string ToJSON() 
          {
            List<string> keywordJSON = new List<string>();
            foreach (string keyword in keywords)
            {
              keywordJSON.Add("{\"keyword\":\"" +keyword+ "\",\"synonyms\":[\""+keyword+"\"]}");
            }
            return "{\"" +entity+ "\":["+ string.Join(",", keywordJSON) + "]}";
          }
    }
}