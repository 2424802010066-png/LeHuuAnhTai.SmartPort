using System;

namespace SmartPort.BuildingBlocks.DoBen
{
    /// <summary>
    /// SU KIEN DEN TRUOC BAN SAO: bo xu ly can mot dong du lieu cua dich vu khac (vd luot xe XeRaVao cua Cong, container
    /// cua Bai) ma ban sao cuc bo CHUA toi. Day la loi TAM THOI (PhanLoaiLoi.LaTamThoi = true): cho lui dan roi thu lai,
    /// KHONG tinh vao ngan sach so lan thu. Truoc day nem InvalidOperationException -> bi xep VINH VIEN -> 6 lan x 3 giay
    /// roi vao hang doi loi, trong khi tin ban sao (bi loi tam thoi luc SQL khoi dong lai) con dang cho o bac 300 giay
    /// -> phi cong khong thu cho toi khi Admin phat lai tay.
    /// </summary>
    public sealed class ChuaCoBanSaoException : Exception
    {
        public ChuaCoBanSaoException(string thongBao) : base(thongBao) { }
    }
}
