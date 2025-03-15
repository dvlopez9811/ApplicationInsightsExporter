﻿using System;
using System.Collections.Generic;
using System.Text;

namespace ApplicationInsights
{
    internal static class SemanticConventions
    {
        public const string InstrumentationLibraryName = "ApplicationInsights";

        public const string ScopeAppInsights = "appinsights";
        public const string ScopeProperties = "prop";

        public const string TelemetryType = ScopeAppInsights + ".type";
        public const string DependencyType = ScopeAppInsights+".dependencytype";
        public const string OperationName = ScopeAppInsights + ".operationname";
        public const string Name = ScopeAppInsights + ".name";
        public const string Url = ScopeAppInsights + ".url";
        public const string Status = ScopeAppInsights + ".status";
        public const string Data = ScopeAppInsights + ".data";
        public const string Source = ScopeAppInsights + ".source";
        public const string Target = ScopeAppInsights + ".target";
        public const string ResultCode = ScopeAppInsights + ".resultcode";
        public const string Properties = "Properties";
        public const string SDKVersionOtel = Properties + ".instrumentationlibrary.version";
        public const string SDKNameOtel = Properties + ".instrumentationlibrary.name";        

    }
}
