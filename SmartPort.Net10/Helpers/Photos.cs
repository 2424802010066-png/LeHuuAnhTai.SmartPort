using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using SmartPort.Models;

namespace SmartPort.Helpers
{
    /// <summary>
    /// Thu vien anh cua website — doc tu bang CauHinhAnh (quan ly tai
    /// Admin > Thu vien anh), co cache trong bo nho de khong query moi request.
    /// Ho tro ca URL ngoai (https://...) lan anh upload noi bo (~/Content/img/...).
    /// Neu CSDL loi/thieu bang → tu dong dung bo anh mac dinh.
    /// </summary>
    public static class Photos
    {
        private static readonly object Lock = new object();
        private static Dictionary<string, string> _cache;

        private const string P = "https://images.unsplash.com/";

        private static readonly Dictionary<string, string> Defaults = new Dictionary<string, string>
        {
            { "hero",   P + "photo-1494412574643-ff11b0a5c1c3?q=80&w=1800&auto=format&fit=crop" },
            { "slide2", P + "photo-1578575437130-527eed3abbec?q=80&w=1800&auto=format&fit=crop" },
            { "slide3", P + "photo-1502920514313-52581002a659?q=80&w=1800&auto=format&fit=crop" },
            { "slide4", P + "photo-1605732562742-3023a888e56e?q=80&w=1800&auto=format&fit=crop" },
            { "head",   P + "photo-1493946740644-2d8a1f1a6aff?q=80&w=1600&auto=format&fit=crop" },
            { "about",  P + "photo-1493946740644-2d8a1f1a6aff?q=80&w=1200&auto=format&fit=crop" },
            { "g1",   P + "photo-1578575437130-527eed3abbec?q=80&w=1200&auto=format&fit=crop" },
            { "g2",   P + "photo-1516216628859-9bccecab13ca?q=80&w=1200&auto=format&fit=crop" },
            { "g3",   P + "photo-1601584115197-04ecc0da31d7?q=80&w=1200&auto=format&fit=crop" },
            { "g4",   P + "photo-1605745341112-85968b19335b?q=80&w=1200&auto=format&fit=crop" },
            { "g5",   P + "photo-1586528116311-ad8dd3c8310d?q=80&w=1200&auto=format&fit=crop" },
            { "g6",   P + "photo-1580674285054-bed31e145f59?q=80&w=1200&auto=format&fit=crop" },
            { "gal1", P + "photo-1605732562742-3023a888e56e?q=80&w=1200&auto=format&fit=crop" },
            { "gal2", P + "photo-1578575437130-527eed3abbec?q=80&w=1200&auto=format&fit=crop" },
            { "gal3", P + "photo-1524055988636-436cfa46e59e?q=80&w=1200&auto=format&fit=crop" },
            { "gal4", P + "photo-1553413077-190dd305871c?q=80&w=1200&auto=format&fit=crop" },
            { "gal5", P + "photo-1519003722824-194d4455a60c?q=80&w=1200&auto=format&fit=crop" },
            { "gal6", P + "photo-1601584115197-04ecc0da31d7?q=80&w=1200&auto=format&fit=crop" },
            { "biz1", P + "photo-1473445361085-b9a07f55608b?q=80&w=800&auto=format&fit=crop" },
            { "biz2", P + "photo-1578574577315-3fbeb0cecdc2?q=80&w=800&auto=format&fit=crop" },
            { "cta",  P + "photo-1502920514313-52581002a659?q=80&w=1600&auto=format&fit=crop" }
        };

        private static Dictionary<string, string> Cache
        {
            get
            {
                if (_cache != null) return _cache;
                lock (Lock)
                {
                    if (_cache != null) return _cache;
                    var map = new Dictionary<string, string>(Defaults);
                    try
                    {
                        using (var db = new AppDb())
                        {
                            foreach (var row in db.CauHinhAnhs.ToList())
                                map[row.Khoa] = row.Url;
                        }
                    }
                    catch { /* thieu bang -> dung mac dinh */ }
                    _cache = map;
                    return _cache;
                }
            }
        }

        /// <summary>Xoa cache sau khi admin doi anh</summary>
        public static void Invalidate()
        {
            lock (Lock) { _cache = null; }
        }

        public static string Url(string key)
        {
            string v;
            if (!Cache.TryGetValue(key ?? "", out v)) v = Defaults["g1"];
            if (v.StartsWith("~"))
            {
                if (v.StartsWith("~/")) v = "/" + v.Substring(2);   // app chay o goc site
            }
            return v;
        }

        /// <summary>Style background 2 lop: anh that + gradient du phong</summary>
        public static string Bg(string key)
        {
            return "background-image:url('" + Url(key) + "'),linear-gradient(135deg,#0d3b66,#1173c4);background-size:cover;background-position:center;";
        }
    }
}
