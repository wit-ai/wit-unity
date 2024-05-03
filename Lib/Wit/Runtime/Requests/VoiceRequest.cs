/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using System.Collections;
using System.Text;
using Meta.Voice.Logging;
using Meta.WitAi;
using Meta.WitAi.Data;
using UnityEngine.Events;

namespace Meta.Voice
{
    /// <summary>
    /// Abstract class for all voice requests
    /// </summary>
    /// <typeparam name="TRequest">The type of request to be returned in event callbacks</typeparam>
    /// <typeparam name="TOptions">The type containing all specific options to be passed to the end service.</typeparam>
    /// <typeparam name="TEvents">The type containing all events of TSession to be called throughout the lifecycle of the request.</typeparam>
    /// <typeparam name="TResults">The type containing all data that can be returned from the end service.</typeparam>
    [LogCategory(LogCategory.Requests)]
    public abstract class VoiceRequest<TUnityEvent, TOptions, TEvents, TResults>
        where TUnityEvent : UnityEventBase
        where TOptions : IVoiceRequestOptions
        where TEvents : VoiceRequestEvents<TUnityEvent>
        where TResults : IVoiceRequestResults
    {
        private readonly IVLogger _log = LoggerRegistry.Instance.GetLogger();

        #region SIMULATION
        public static SimulatedResponse simulatedResponse;
        #endregion

        /// <summary>
        /// The states of a voice request
        /// </summary>
        public VoiceRequestState State { get; private set; } = (VoiceRequestState) (-1);
        /// <summary>
        /// Active if not currently canceled, failed or successful
        /// </summary>
        public bool IsActive => State == (VoiceRequestState)(-1)
                                || State == VoiceRequestState.Initialized
                                || State == VoiceRequestState.Transmitting;

        /// <summary>
        /// Whether transmission should hold prior to send
        /// </summary>
        public bool OnHold = false;

        /// <summary>
        /// Download progress of the current request transmission
        /// if available
        /// </summary>
        public float DownloadProgress { get; private set; } = 0f;
        /// <summary>
        /// Upload progress of the current request transmission
        /// if available
        /// </summary>
        public float UploadProgress { get; private set; } = 0f;

        /// <summary>
        /// Options sent as the request parameters
        /// </summary>
        public TOptions Options { get; }
        /// <summary>
        /// Events specific to this voice request
        /// </summary>
        public TEvents Events { get; }
        /// <summary>
        /// Results returned from the request
        /// </summary>
        public TResults Results { get; }

        /// <summary>
        /// Whether request can currently transmit data
        /// </summary>
        public bool CanSend => string.IsNullOrEmpty(GetSendError());

        #region INITIALIZATION
        /// <summary>
        /// Constructor class for voice requests
        /// </summary>
        /// <param name="newOptions">The request parameters to be used</param>
        /// <param name="newEvents">The request events to be called throughout it's lifecycle</param>
        public VoiceRequest(TOptions newOptions, TEvents newEvents)
        {
            // Apply options if they exist, otherwise generate
            Options = newOptions != null ? newOptions : Activator.CreateInstance<TOptions>();
            // Generate new events & add parameter events as listeners
            Events = Activator.CreateInstance<TEvents>();
            if (newEvents != null)
            {
                AddEventListeners(newEvents);
            }
            // Generate results and apply changes throughout lifecycle
            Results = GetNewResults();

            // Initialized
            SetState(VoiceRequestState.Initialized);
        }

        /// <summary>
        /// Method for generating results, can be overwritten for custom generation
        /// </summary>
        protected virtual TResults GetNewResults() =>
            Activator.CreateInstance<TResults>();

        /// <summary>
        /// Adds listeners for all events provided
        /// </summary>
        public void AddEventListeners(TEvents newEvents) =>
            SetEventListeners(newEvents, true);

        /// <summary>
        /// Removes listeners for all events provided
        /// </summary>
        public void RemoveEventListeners(TEvents newEvents) =>
            SetEventListeners(newEvents, false);

        /// <summary>
        /// Subscribes or unsubscribes all provided events from this request's
        /// Events callbacks.
        /// </summary>
        /// <param name="newEvents">The events to subscribe or unsubscribe to the request.Events</param>
        /// <param name="addListeners">Whether to add listeners or remove listeners</param>
        protected abstract void SetEventListeners(TEvents newEvents, bool addListeners);

        /// <summary>
        /// Raises a voice request event
        /// </summary>
        /// <param name="requestEvent">Event to be performed</param>
        protected abstract void RaiseEvent(TUnityEvent requestEvent);

        /// <summary>
        /// Call after initialization
        /// </summary>
        protected virtual void OnInit()
        {
            RaiseEvent(Events?.OnInit);
            SetUploadProgress(0f);
            SetDownloadProgress(0f);
        }
        /// <summary>
        /// Apply the voice request state
        /// </summary>
        protected virtual void SetState(VoiceRequestState newState)
        {
            // Ignore same state
            if (State == newState)
            {
                return;
            }

            // Set state & update event
            State = newState;
            OnStateChange();

            // Handle completion
            bool shouldComplete = false;

            // Perform state specific event
            switch (State)
            {
                case VoiceRequestState.Initialized:
                    try
                    {
                        OnInit();
                    }
                    catch (Exception e)
                    {
                        LogE("OnInit Exception Caught", e);
                    }
                    break;
                case VoiceRequestState.Transmitting:
                    CoroutineUtility.StartCoroutine(WaitForHold(OnCanSend));
                    break;
                case VoiceRequestState.Canceled:
                    try
                    {
                        HandleCancel();
                    }
                    catch (Exception e)
                    {
                        LogE("HandleCancel Exception Caught", e);
                    }
                    try
                    {
                        OnCancel();
                    }
                    catch (Exception e)
                    {
                        LogE("OnCancel Exception Caught", e);
                    }
                    shouldComplete = true;
                    break;
                case VoiceRequestState.Failed:
                    try
                    {
                        OnFailed();
                    }
                    catch (Exception e)
                    {
                        LogE("OnFailed Exception Caught", e);
                    }
                    shouldComplete = true;
                    break;
                case VoiceRequestState.Successful:
                    try
                    {
                        OnSuccess();
                    }
                    catch (Exception e)
                    {
                        LogE("OnSuccess Exception Caught", e);
                    }
                    shouldComplete = true;
                    break;
            }

            // Handle completion
            if (shouldComplete)
            {
                try
                {
                    OnComplete();
                }
                catch (Exception e)
                {
                    LogE("OnComplete Exception Caught", e);
                }
            }
        }

        /// <summary>
        /// Method for calling state change delegates
        /// </summary>
        protected virtual void OnStateChange() =>
            RaiseEvent(Events?.OnStateChange);

        // Wait for hold to complete and then perform an action
        protected virtual IEnumerator WaitForHold(Action onReady)
        {
            while (OnHold)
            {
                yield return null;
            }
            onReady?.Invoke();
        }

        // Once hold is complete, begin send process
        protected virtual void OnCanSend()
        {
            if (State != VoiceRequestState.Transmitting)
            {
                return;
            }
            try
            {
                OnSend();
            }
            catch (Exception e)
            {
                LogE("OnSend Exception Caught", e);
            }
            if (!OnSimulateResponse())
            {
                try
                {
                    HandleSend();
                }
                catch (Exception e)
                {
                    LogE("HandleSend Exception Caught", e);
                }
            }
        }

        /// <summary>
        /// Set current request download progress
        /// </summary>
        /// <param name="newProgress">New progress value</param>
        protected void SetDownloadProgress(float newProgress)
        {
            // Ignore same progress
            if (DownloadProgress.Equals(newProgress))
            {
                return;
            }

            // Set progress & update event
            DownloadProgress = newProgress;
            RaiseEvent(Events?.OnDownloadProgressChange);
        }
        /// <summary>
        /// Set current request upload progress
        /// </summary>
        /// <param name="newProgress">New progress value</param>
        protected void SetUploadProgress(float newProgress)
        {
            // Ignore same progress
            if (UploadProgress.Equals(newProgress))
            {
                return;
            }

            // Set progress & update event
            UploadProgress = newProgress;
            RaiseEvent(Events?.OnUploadProgressChange);
        }

        /// <summary>
        /// Internal method for
        /// </summary>
        protected void Log(string log, VLoggerVerbosity logLevel = VLoggerVerbosity.Info)
        {
            // Start log
            StringBuilder requestLog = new StringBuilder();
            // Append sent log
            requestLog.AppendLine(log);
            // Append any request specific data
            AppendLogData(requestLog, logLevel);
            // Log
            _log.Log(_log.CorrelationID, logLevel, log);
        }
        protected void LogW(string log) => Log(log, VLoggerVerbosity.Warning);
        protected void LogE(string log, Exception e) => Log($"{log}\n\n{e}", VLoggerVerbosity.Error);

        /// <summary>
        /// Append request specific data to log
        /// </summary>
        /// <param name="log">Building log</param>
        /// <param name="warning">True if this is a warning log</param>
        protected virtual void AppendLogData(StringBuilder log, VLoggerVerbosity logLevel)
        {
            #if UNITY_EDITOR
            // Append request id
            log.AppendLine($"Request Id: {Options?.RequestId}");
            #endif
            // Append request state
            log.AppendLine($"Request State: {State}");
        }
        #endregion INITIALIZATION

        #region TRANSMISSION
        /// <summary>
        /// Internal way to determine send error
        /// </summary>
        protected virtual string GetSendError()
        {
            // Cannot send if not initialized
            if (State != VoiceRequestState.Initialized)
            {
                return $"Cannot send request in '{State}' state.";
            }
            // Cannot send without valid request id
            if (string.IsNullOrEmpty(Options?.RequestId))
            {
                return $"Cannot send request without a request id.";
            }
            // Send allowed
            return string.Empty;
        }
        /// <summary>
        /// Public request to transmit data
        /// </summary>
        public virtual void Send()
        {
            // Warn & ignore
            if (State != VoiceRequestState.Initialized)
            {
                LogW($"Request Send Ignored\nReason: Invalid state");
                return;
            }

            // Fail if cannot send
            string sendError = GetSendError();
            if (!string.IsNullOrEmpty(sendError))
            {
                HandleFailure(sendError);
                return;
            }

            // Set to transmitting state
            SetState(VoiceRequestState.Transmitting);
        }

        /// <summary>
        /// Call after transmission begins
        /// </summary>
        protected virtual void OnSend()
        {
            // Call send event
            Log($"Request Transmitting");
            RaiseEvent(Events?.OnSend);
        }

        /// <summary>
        /// Child class send implementation
        /// Call HandleFailure, HandleCancel from this class
        /// </summary>
        protected abstract void HandleSend();

        /// <summary>
        ///
        /// </summary>
        /// <returns>True if the response was simulated</returns>
        protected virtual bool OnSimulateResponse()
        {
            return false;
        }
        #endregion TRANSMISSION

        #region RESULTS
        /// <summary>
        /// Method for handling failure with only an error string
        /// </summary>
        /// <param name="error">The error to be returned</param>
        protected virtual void HandleFailure(string error) =>
            HandleFailure(WitConstants.ERROR_CODE_GENERAL, error);

        /// <summary>
        /// Method for handling failure that takes an error status code or an error itself
        /// </summary>
        /// <param name="errorStatusCode">The error status code if applicable</param>
        /// <param name="errorMessage">The error to be returned</param>
        protected virtual void HandleFailure(int errorStatusCode, string errorMessage)
        {
            // Ignore if not in correct state
            if (!IsActive)
            {
                LogW($"Request Failure Ignored\nReason: Request is already complete");
                return;
            }
            // Cancel immediately
            if (string.Equals(WitConstants.CANCEL_ERROR, errorMessage))
            {
                Cancel();
                return;
            }
            // Assume success
            if (ShouldIgnoreError(errorStatusCode, errorMessage))
            {
                HandleSuccess();
                return;
            }

            // Apply results with error
            Results.SetError(errorStatusCode, errorMessage);

            // Set failure state
            SetState(VoiceRequestState.Failed);
        }

        /// <summary>
        /// Check for ignored error status codes & messages.
        /// </summary>
        /// <param name="errorStatusCode">The error status code if applicable</param>
        /// <param name="errorMessage">The error to be returned</param>
        protected virtual bool ShouldIgnoreError(int errorStatusCode, string errorMessage)
        {
            return string.IsNullOrEmpty(errorMessage);
        }

        /// <summary>
        /// Call after failure state set
        /// </summary>
        protected virtual void OnFailed()
        {
            RaiseEvent(Events?.OnFailed);
        }

        /// <summary>
        /// Method for handling success with a full result object
        /// </summary>
        /// <param name="error">The error to be returned</param>
        protected virtual void HandleSuccess()
        {
            // Ignore if not in correct state
            if (!IsActive)
            {
                LogW($"Request Success Ignored\nReason: Request is already complete");
                return;
            }

            // Set success state
            SetState(VoiceRequestState.Successful);
        }
        /// <summary>
        /// Call after success state set
        /// </summary>
        protected virtual void OnSuccess()
        {
            Log($"Request Success\nResults: {Results != null}");
            RaiseEvent(Events?.OnSuccess);
        }

        /// <summary>
        /// Cancel the request immediately
        /// </summary>
        public virtual void Cancel(string reason = WitConstants.CANCEL_MESSAGE_DEFAULT)
        {
            // Ignore if cannot cancel
            if (!IsActive)
            {
                LogW($"Request Cancel Ignored\nReason: Request is already complete");
                return;
            }

            // Set cancellation reason
            Results.SetCancel(reason);

            // Set cancellation state
            SetState(VoiceRequestState.Canceled);
        }

        /// <summary>
        /// Handle cancelation in subclass
        /// </summary>
        protected abstract void HandleCancel();

        /// <summary>
        /// Call after cancellation state set
        /// </summary>
        protected virtual void OnCancel()
        {
            // Log & callbacks
            RaiseEvent(Events?.OnCancel);
        }

        /// <summary>
        /// Call after failure, success or cancellation
        /// </summary>
        protected virtual void OnComplete()
        {
            RaiseEvent(Events?.OnComplete);
        }
        #endregion RESULTS

        #region THREAD SAFETY
        // Called from background thread
        protected void MainThreadCallback(Action action)
            => ThreadUtility.CallOnMainThread(action);
        #endregion THREAD SAFETY
    }
}
