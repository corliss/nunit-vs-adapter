﻿// ****************************************************************
// Copyright (c) 2013 NUnit Software. All rights reserved.
// ****************************************************************

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;

namespace NUnit.VisualStudio.TestAdapter
{
    using System.Collections;

    public class TFSTestFilter
    {
        /// <summary>   
        /// Supported properties for filtering

        ///</summary>
        private static readonly Dictionary<string, TestProperty> supportedPropertiesCache;
        private static readonly Dictionary<string, NTrait> supportedTraitCache;
        private static readonly Dictionary<NTrait, TestProperty> traitPropertyMap;
        private static readonly List<string> supportedProperties;

        static TFSTestFilter()
        {
            // Initialize the property cache
            supportedPropertiesCache = new Dictionary<string, TestProperty>(StringComparer.OrdinalIgnoreCase);
            supportedPropertiesCache["FullyQualifiedName"] = TestCaseProperties.FullyQualifiedName;
            supportedPropertiesCache["Name"] = TestCaseProperties.DisplayName;
            // Intialize the trait cache
            var priorityTrait = new NTrait("Priority", "");
            var categoryTrait = new NTrait("Category", "");
            supportedTraitCache = new Dictionary<string, NTrait>(StringComparer.OrdinalIgnoreCase);
            supportedTraitCache["Priority"] = priorityTrait;
            supportedTraitCache["TestCategory"] = categoryTrait;
            // Initalize the trait property map, since TFS doesnt know about traits
            traitPropertyMap = new Dictionary<NTrait, TestProperty>(new NTraitNameComparer());
            var priorityProperty = TestProperty.Find("Priority") ??
                      TestProperty.Register("Priority", "Priority", typeof(string), typeof(TestCase));
            traitPropertyMap[priorityTrait] = priorityProperty;
            var categoryProperty = TestProperty.Find("TestCategory") ??
                        TestProperty.Register("TestCategory", "TestCategory", typeof(string), typeof(TestCase));
            traitPropertyMap[categoryTrait] = categoryProperty;
            // Initialize a merged list of properties and traits to fool TFS Build to think traits is properties
            supportedProperties = new List<string>();
            supportedProperties.AddRange(supportedPropertiesCache.Keys);
            supportedProperties.AddRange(supportedTraitCache.Keys);
        }

        private IRunContext runContext;
        public TFSTestFilter(IRunContext runContext)
        {
            this.runContext = runContext;
        }


        private ITestCaseFilterExpression testCaseFilterExpression;
        public ITestCaseFilterExpression TfsTestCaseFilterExpression
        {
            get
            {
                return testCaseFilterExpression ??
                       (testCaseFilterExpression = runContext.GetTestCaseFilter(supportedProperties, PropertyProvider));
            }
        }

        public bool HasTfsFilterValue
        {
            get
            {
                return TfsTestCaseFilterExpression != null && TfsTestCaseFilterExpression.TestCaseFilterValue != String.Empty;
            }
        }
        public IEnumerable<TestCase> CheckFilter(IEnumerable<TestCase> tests)
        {

            return TfsTestCaseFilterExpression == null ? tests : tests.Where(underTest => !TfsTestCaseFilterExpression.MatchTestCase(underTest, p => PropertyValueProvider(underTest, p)) == false).ToList();
        }

        /// <summary>    
        /// Provides value of TestProperty corresponding to property name 'propertyName' as used in filter.    
        /// /// Return value should be a string for single valued property or array of strings for multi valued property (e.g. TestCategory)   
        /// /// </summary>     
        public static object PropertyValueProvider(TestCase currentTest, string propertyName)
        {

            var testProperty = LocalPropertyProvider(propertyName);
            if (testProperty != null)
            {

                // Test case might not have defined this property. In that case GetPropertyValue()             
                // would return default value. For filtering, if property is not defined return null.           
                if (currentTest.Properties.Contains(testProperty))
                {
                    return currentTest.GetPropertyValue(testProperty);
                }
            }
            // Now it may be a trait, so we check the trait collection as well
            var testTrait = TraitProvider(propertyName);
            if (testTrait != null)
            {
                var val = traitContains(currentTest, testTrait.Name);
                if (val.Length == 0) return null;
                if (val.Length == 1) // Contains a single string
                    return val[0];  // return that string
                return val;  // otherwise return the whole array 
            }
            return null;
        }

        static readonly Func<TestCase, string, string[]> traitContains = TraitContains();

        /// <summary>
        /// TestCase:  To be checked
        /// traitName: Name of trait to be checked against
        /// </summary>
        /// <returns>Value of trait</returns>
        private static Func<TestCase, string, string[]> TraitContains()
        {

            return (testCase, traitName) =>
            {
                var testCaseType = typeof(TestCase);
                var property = testCaseType.GetProperty("Traits");
                if (property == null)
                    return null;
                var traits = property.GetValue(testCase, null) as IEnumerable;
                var values = new List<string>();
                foreach (var t in traits)
                {
                    var name = t.GetType().GetProperty("Name").GetValue(t, null) as string;
                    if (name == traitName)
                    {
                        var value = t.GetType().GetProperty("Value").GetValue(t, null) as string;
                        values.Add(value);
                    }
                }
                return values.ToArray();
            };
        }

        /// <summary>   
        /// Provides TestProperty for property name 'propertyName' as used in filter.    
        /// </summary>    
        public static TestProperty LocalPropertyProvider(string propertyName)
        {
            TestProperty testProperty;
            supportedPropertiesCache.TryGetValue(propertyName, out testProperty);
            return testProperty;
        }

        public static TestProperty PropertyProvider(string propertyName)
        {
            var testProperty = LocalPropertyProvider(propertyName);
            if (testProperty != null)
            {
                return testProperty;
            }
            var testTrait = TraitProvider(propertyName);
            if (testTrait != null)
            {
                TestProperty tp;
                if (traitPropertyMap.TryGetValue(testTrait, out tp))
                {
                    return tp;
                }
            }
            return null;
        }

        public static NTrait TraitProvider(string traitName)
        {
            NTrait testTrait;
            supportedTraitCache.TryGetValue(traitName, out testTrait);
            return testTrait;
        }



    }

    public class NTraitNameComparer : IEqualityComparer<NTrait>
    {

        public bool Equals(NTrait n, NTrait y)
        {
            return n.Name == y.Name;
        }

        public int GetHashCode(NTrait obj)
        {
            return obj.Name.GetHashCode();
        }
    }
}
