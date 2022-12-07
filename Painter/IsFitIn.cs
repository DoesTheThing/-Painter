using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Painter
{
    class IsFitIn
    {
        public bool NTop = true; //is not top ovrfl
        public bool NLeft = true; //is not left ovrfl

        public bool FullyFit 
        {
            get 
            {
                return NLeft && NTop; 
            }
        }
    }
}
