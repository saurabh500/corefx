// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Data.SqlClient.SNI;

namespace System.Data.SqlClient
{
    internal sealed partial class TdsParser
    {

        private SNIErrorDetails GetSniErrorDetails()
        {
            SNIErrorDetails details = new SNIErrorDetails();

            SNIError sniError = SNIProxy.Singleton.GetLastError();
            details.sniErrorNumber = sniError.sniError;
            details.errorMessage = sniError.errorMessage;
            details.nativeError = sniError.nativeError;
            details.provider = (int)sniError.provider;
            details.lineNumber = sniError.lineNumber;
            details.function = sniError.function;
            details.exception = sniError.exception;
            
            
            return details;
        }

    }    // tdsparser
}//namespace
