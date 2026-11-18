# Dữ liệu thật — Sản lượng hàng hóa thông qua cảng biển Việt Nam (1/2021 – 6/2026)

Bộ dữ liệu dùng trong báo cáo `BaoCao_SmartPort_DuBaoContainer_LeHuuAnhTai_2424802010066.docx`.

## Nguồn

- **Cục Hàng hải và Đường thủy Việt Nam** — mục Thống kê: https://vimawa.gov.vn/vi/thong-ke
- 55 tệp báo cáo tháng (Biểu số 28-T / 07-T), kỳ 11/2020 → 6/2026, tải ngày 17/09/2026:
  xlsx (2020, và 2 workbook “Biểu 28-2025/2026” có sheet T1…T12 + cả năm), PDF có chữ (7/2021–3/2023),
  PDF scan (1–6/2021, 4–11/2023), docx (12/2023–5/2025, báo cáo năm 2023).
- Tệp gốc KHÔNG lưu trong repo (tải lại từ trang trên nếu cần).

## Tệp trong thư mục

| Tệp | Nội dung |
|---|---|
| `container_vn_thang.csv` | 66 tháng × 19 trường (nghìn TEU, nghìn tấn) — dữ liệu đã làm sạch |
| `vimawa_nhap_tay.csv` | 44 dòng số liệu đọc thủ công từ 2 trang scan (báo cáo 2/2021 và 6/2023) |
| `thuc_nghiem.py` | Thực nghiệm MLP (cấu hình AI Lab) + mô hình đối chứng; ghi `ket_qua_thuc_nghiem.json` |

## Quy tắc làm sạch (để trả lời vấn đáp)

1. **Không dùng cột “Ước thực hiện tháng”** — là số ước giữa tháng, nhiều kỳ chỉ ngoại suy thô
   (ví dụ 11/2020 ước 1.812, thực tế 2.174 nghìn TEU).
2. **Số tháng = lũy kế tháng này − lũy kế tháng trước.** Lũy kế thực lấy từ cột “Từ đầu năm đến hết tháng trước”
   của báo cáo tháng sau, cột “Lũy kế cùng kỳ năm trước” của báo cáo năm sau, hoặc báo cáo năm.
3. **Nhiều nguồn → lấy bản công bố muộn nhất** (đã hiệu chỉnh). Số liệu 2024 được công bố lại cao hơn ~2%
   từ tháng 5/2024 (cả năm: 29.925 → 30.571 nghìn TEU).
4. **Không trộn hai lần công bố khi lấy hiệu** (tránh đỉnh giả).
5. Kiểm tra: CONT_XK_TEU + CONT_NK_TEU + CONT_ND_TEU = CONT_TEU (66/66 tháng, lệch ≤ 1).

## Lưu ý

- Các trường theo **tấn** (TONG, XK, NK, ND, LONG, KHO) bị đổi phạm vi thống kê từ 4/2024 (công bố lại tăng mạnh)
  → không dùng làm đặc trưng mô hình.
- Container nội địa 2/2024 = 390 và 3/2024 = 1.184 nghìn TEU là số trong báo cáo gốc (không phải lỗi đọc).
- `QUACANH` thiếu 4 tháng (5–6/2021, 4–5/2022).
