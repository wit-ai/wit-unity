/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using System.Collections;

namespace Meta.Conduit
{
    /// <summary>
    /// A step in processing or query.
    /// </summary>
    /// <param name="success">True if the step succeeded. False otherwise (with error in data field)</param>
    /// <param name="data">The optional data returned in success or the error data on failure.</param>
    public delegate void StepResult(bool success, string data);

    public interface IProcessStep
    {
        string StepName { get; }

        /// <summary>
        /// Runs the step.
        /// </summary>
        /// <param name="updateProgress"></param>
        /// <param name="completionCallback">Called when the step concludes. First parameter is true on success. The second parameter is the error value if it failed.</param>
        /// <returns></returns>
        IEnumerator Run(Action<String, float> updateProgress, StepResult completionCallback);

        Payload Payload { get; }
    }
}
