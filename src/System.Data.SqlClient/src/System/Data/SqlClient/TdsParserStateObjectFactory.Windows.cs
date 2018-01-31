﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Data.SqlClient.SNI;

namespace System.Data.SqlClient
{
    internal sealed class TdsParserStateObjectFactory
    {

        public static readonly TdsParserStateObjectFactory Singleton = new TdsParserStateObjectFactory();


        // Temporary disabling App Context switching for managed SNI.
        // If the appcontext switch is set then Use Managed SNI based on the value. Otherwise Managed SNI should always be used.
        //private static bool shouldUseLegacyNetorking;
        //public static bool UseManagedSNI { get; } = AppContext.TryGetSwitch(UseLegacyNetworkingOnWindows, out shouldUseLegacyNetorking) ? !shouldUseLegacyNetorking : true;

        
        public EncryptionOptions EncryptionOptions
        {
            get
            {
                return SNI.SNILoadHandle.SingletonInstance.Options;
            }
        }

        public uint SNIStatus
        {
            get
            {
                return SNI.SNILoadHandle.SingletonInstance.Status;
            }
        }

        public TdsParserStateObject CreateTdsParserStateObject(TdsParser parser)
        {
            
            return new TdsParserStateObjectManaged(parser);
            
        }

        internal TdsParserStateObject CreateSessionObject(TdsParser tdsParser, TdsParserStateObject _pMarsPhysicalConObj, bool v)
        {
            return new TdsParserStateObjectManaged(tdsParser, _pMarsPhysicalConObj, true);
        }
    }
}
