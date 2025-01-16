﻿namespace Switchboard.Core
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using WatsonWebserver.Core;

    /// <summary>
    /// Switchboard callbacks.
    /// </summary>
    public class SwitchboardCallbacks
    {
        #region Public-Members

        /// <summary>
        /// Authenticate a request.  This method will pass to your application an HttpContextBase object.  Your application should return a populated AuthenticationResult object.
        /// </summary>
        public Func<HttpContextBase, Task<AuthenticationResult>> Authenticate { get; set; }

        #endregion

        #region Private-Members

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Switchboard callbacks.
        /// </summary>
        public SwitchboardCallbacks()
        {

        }

        #endregion

        #region Public-Methods

        #endregion

        #region Private-Methods

        #endregion
    }
}