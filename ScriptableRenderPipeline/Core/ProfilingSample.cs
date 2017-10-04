﻿using System;
using UnityEngine.Rendering;

namespace UnityEngine.Experimental.Rendering
{
    public struct ProfilingSample : IDisposable
    {
        public readonly CommandBuffer cmd;
        public readonly string name;

        bool m_Disposed;

        public ProfilingSample(string name, CommandBuffer cmd)
        {
            this.cmd = cmd;
            this.name = name;
            m_Disposed = false;
            cmd.BeginSample(name);
        }

        public void Dispose()
        {
            Dispose(true);
        }

        // Protected implementation of Dispose pattern.
        void Dispose(bool disposing)
        {
            if (m_Disposed)
                return;

            // As this is a struct, it could have been initialized using an empty constructor so we
            // need to make sure `cmd` isn't null to avoid a crash. Switching to a class would fix
            // this but will generate garbage on every frame (and this struct is used quite a lot).
            if (disposing && cmd != null)
                cmd.EndSample(name);

            m_Disposed = true;
        }
    }
}
