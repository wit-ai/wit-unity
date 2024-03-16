/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;

namespace Meta.Voice.Logging
{
    /// <summary>
    /// This attributes associates a category name to a class. The category name will be used in logging.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class LogCategoryAttribute : Attribute
    {
        /// <summary>
        /// The name of the category.
        /// </summary>
        public string CategoryName { get; }

        /// <summary>
        /// The name of the parent category if applicable.
        /// The provision of this value constructs a parent-child hierarchy of categories.
        /// </summary>
        public string ParentCategoryName { get; }

        /// <summary>
        /// Constructs the attribute with a category name only and no parent.
        /// </summary>
        /// <param name="categoryName">The category name.</param>
        public LogCategoryAttribute(string categoryName)
        {
            CategoryName = categoryName;
        }

        /// <summary>
        /// Constructs the attribute with a category name in enum form. Used internally by the VSDK.
        /// </summary>
        /// <param name="categoryName">The category name.</param>
        public LogCategoryAttribute(LogCategory categoryName)
        {
            CategoryName = categoryName.ToString();
        }

        /// <summary>
        /// Constructs the attribute with both a category name and parent.
        /// </summary>
        /// <param name="parentCategoryName">The parent category name.</param>
        /// <param name="categoryName">The category name.</param>
        public LogCategoryAttribute(string parentCategoryName, string categoryName)
        {
            ParentCategoryName = parentCategoryName;
            CategoryName = categoryName;
        }

        /// <summary>
        /// An overload that uses the Enum version of the category names.
        /// </summary>
        /// <param name="parentCategoryName">The parent category name.</param>
        /// <param name="categoryName">The category name.</param>
        public LogCategoryAttribute(LogCategory parentCategoryName, LogCategory categoryName) : this(
            parentCategoryName.ToString(), categoryName.ToString())
        {
        }
    }
}
