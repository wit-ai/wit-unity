/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;

namespace Meta.Conduit
{
    public class WitAppInfo
    {
        public string Id { get; set;}

        public string Name { get; set;}

        public string Language { get; set;}

        public bool Private { get; set;}

        public DateTime Created { get; set;}

        public DateTime WillTrain { get; set;}

        public DateTime LastTrained { get; set;}

        public int LastTrainingDuration { get; set;}

        public WitTrainingStatus TrainingStatus { get; set;}
    }
}
