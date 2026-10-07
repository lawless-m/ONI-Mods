using System;
using System.Runtime.InteropServices;

namespace BuildingTrackerMod
{
    public static class Sqlite3
    {
        const string LIB = "sqlite3";

        public const int OK = 0;
        public const int ROW = 100;
        public const int DONE = 101;

        [DllImport(LIB)] public static extern int sqlite3_open(string filename, out IntPtr db);
        [DllImport(LIB)] public static extern int sqlite3_close(IntPtr db);
        [DllImport(LIB)] public static extern int sqlite3_exec(IntPtr db, string sql, IntPtr callback, IntPtr arg, out IntPtr errmsg);
        [DllImport(LIB)] public static extern int sqlite3_prepare_v2(IntPtr db, string sql, int nByte, out IntPtr stmt, IntPtr pzTail);
        [DllImport(LIB)] public static extern int sqlite3_step(IntPtr stmt);
        [DllImport(LIB)] public static extern int sqlite3_finalize(IntPtr stmt);
        [DllImport(LIB)] public static extern int sqlite3_reset(IntPtr stmt);
        [DllImport(LIB)] public static extern int sqlite3_bind_int(IntPtr stmt, int index, int value);
        [DllImport(LIB)] public static extern int sqlite3_bind_double(IntPtr stmt, int index, double value);
        [DllImport(LIB)] public static extern int sqlite3_bind_text(IntPtr stmt, int index, string value, int nByte, IntPtr destructor);
        [DllImport(LIB)] public static extern void sqlite3_free(IntPtr ptr);
        [DllImport(LIB)] public static extern IntPtr sqlite3_errmsg(IntPtr db);

        public static readonly IntPtr SQLITE_TRANSIENT = new IntPtr(-1);

        public static void Check(IntPtr db, int result)
        {
            if (result != OK)
            {
                string msg = Marshal.PtrToStringAnsi(sqlite3_errmsg(db));
                throw new Exception($"sqlite3 error {result}: {msg}");
            }
        }

        public static void Exec(IntPtr db, string sql)
        {
            int rc = sqlite3_exec(db, sql, IntPtr.Zero, IntPtr.Zero, out IntPtr errmsg);
            if (rc != OK)
            {
                string msg = Marshal.PtrToStringAnsi(errmsg);
                sqlite3_free(errmsg);
                throw new Exception($"sqlite3_exec error {rc}: {msg}");
            }
        }

        public static void BindText(IntPtr stmt, int index, string value)
        {
            sqlite3_bind_text(stmt, index, value, -1, SQLITE_TRANSIENT);
        }
    }
}
