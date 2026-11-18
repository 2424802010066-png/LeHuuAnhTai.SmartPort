import { LOP_LOAI_BAN_TIN } from './chung'

/** Nhan mau loai ban tin EDI: CODECO xanh duong · COARRI xanh ngoc · COPARN tim */
export default function NhanLoaiBanTin({ loai }) {
  return <span className={'ed-loai ' + (LOP_LOAI_BAN_TIN[loai] || '')}>{loai || '—'}</span>
}
