using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MediaElementMemstream
{
    public static class AsyncErrorHandler
    {
        public static void HandleException(Exception ex)
        {
            if (ex != null)
            {

                System.Diagnostics.Debug.WriteLine("== Error in task in MediaElementMemstream ==");
                System.Diagnostics.Debug.WriteLine(ex.Message);
                System.Diagnostics.Debug.WriteLine(ex.StackTrace);
                HandleException(ex.InnerException);
            }
        }
    }
}
