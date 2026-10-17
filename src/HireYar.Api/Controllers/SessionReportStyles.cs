namespace HireYar.Api.Controllers;

internal static class SessionReportStyles
{
    internal const string Css = """

* { box-sizing:border-box; margin:0; padding:0; }
body { font-family:'Vazirmatn', Tahoma, sans-serif; background:#07111e; color:#d7e2ee; font-size:14px; line-height:1.9; padding-bottom:70px; }

/* topbar */
.topbar { display:flex; justify-content:space-between; align-items:center; padding:14px 28px; background:#0a1727; border-bottom:1px solid #16293f; position:sticky; top:0; z-index:50; }
.topbar .brand { font-weight:bold; font-size:17px; color:#f1f5f9; }
.topbar .brand span { color:#10b981; }
.crumb { color:#5d7691; font-size:12.5px; }
.tb-actions { display:flex; gap:10px; }
.btn { padding:8px 18px; border-radius:9px; border:1px solid #27486b; background:transparent; color:#8ea9c4; font-family:inherit; font-size:13px; font-weight:600; cursor:pointer; }
.btn:hover { border-color:#3d6a96; color:#d7e2ee; }
.btn-p { background:#10b981; border-color:#10b981; color:#03211d; }
.btn-p:hover { background:#34d399; color:#03211d; }

.wrap { max-width:1180px; margin:0 auto; padding:26px 22px; }

/* report type tabs */
.rtabs { display:flex; gap:8px; margin-bottom:22px; }
.rtab { padding:10px 24px; border-radius:11px; background:#0d1c2e; border:1px solid #1d3548; color:#8ea9c4; font-weight:700; font-size:13.5px; cursor:pointer; font-family:inherit; }
.rtab.on { background:rgba(16,185,129,.12); border-color:#10b981; color:#34d399; }
.report { display:none; }
.report.on { display:block; animation:fade .3s ease; }
@keyframes fade { from{opacity:0; transform:translateY(6px)} to{opacity:1; transform:none} }

/* hero */
.hero { display:grid; grid-template-columns:1.25fr .9fr 1fr; gap:16px; margin-bottom:16px; }
.card { background:#0d1c2e; border:1px solid #1d3548; border-radius:16px; padding:20px 22px; }
.card h3 { font-size:15px; color:#e8f1f8; margin-bottom:12px; display:flex; align-items:center; gap:8px; }
.card h3 .tag { font-size:11px; font-weight:400; color:#5d7691; }

.cand { display:flex; gap:14px; align-items:center; }
.cand .ava { width:58px; height:58px; border-radius:50%; background:linear-gradient(135deg,#1e3a5f,#0f766e); display:flex; align-items:center; justify-content:center; font-size:22px; font-weight:bold; color:#a5f3fc; }
.cand .nm { font-size:18px; font-weight:bold; color:#f1f5f9; }
.cand .ps { color:#8ea9c4; font-size:13px; }
.badges { display:flex; gap:8px; flex-wrap:wrap; margin-top:14px; }
.bdg { font-size:11.5px; border-radius:16px; padding:3px 12px; background:#0a1727; border:1px solid #27486b; color:#8ea9c4; }
.bdg.g { background:rgba(16,185,129,.1); border-color:rgba(16,185,129,.45); color:#6ee7b7; }
.bdg.b { background:rgba(56,189,248,.1); border-color:rgba(56,189,248,.45); color:#7dd3fc; }
.bdg.p { background:rgba(167,139,250,.1); border-color:rgba(167,139,250,.45); color:#c4b5fd; }

/* donut */
.donut-box { display:flex; flex-direction:column; align-items:center; justify-content:center; }
.donut {
    width:150px; height:150px; border-radius:50%; position:relative;
    background:conic-gradient(#10b981 0 82%, #16293f 82% 100%);
}
.donut::after {
    content:''; position:absolute; inset:16px; border-radius:50%; background:#0d1c2e;
}
.donut .val { position:absolute; inset:0; display:flex; flex-direction:column; align-items:center; justify-content:center; z-index:2; }
.donut .val b { font-size:34px; color:#f1f5f9; line-height:1.2; }
.donut .val span { font-size:11px; color:#5d7691; }
.donut-label { margin-top:10px; font-size:14px; font-weight:700; color:#34d399; }
.donut-sub { font-size:11.5px; color:#5d7691; }

/* fit */
.fit .verdict { display:flex; align-items:center; gap:10px; background:rgba(16,185,129,.1); border:1px solid rgba(16,185,129,.4); border-radius:12px; padding:12px 14px; margin-bottom:10px; }
.fit .verdict .ic { font-size:24px; }
.fit .verdict .t { font-weight:bold; color:#6ee7b7; font-size:15px; }
.fit .verdict .d { font-size:11.5px; color:#8fd6b8; }
.meter { height:9px; background:#16293f; border-radius:6px; overflow:hidden; margin:6px 0 4px; }
.meter i { display:block; height:100%; background:linear-gradient(90deg,#10b981,#34d399); border-radius:6px; }
.fit .row { font-size:12.5px; color:#8ea9c4; display:flex; justify-content:space-between; }

/* kpis */
.kpis { display:grid; grid-template-columns:repeat(4,1fr); gap:14px; margin-bottom:16px; }
.kpi { background:#0d1c2e; border:1px solid #1d3548; border-radius:14px; padding:14px 16px; }
.kpi .l { font-size:11.5px; color:#5d7691; }
.kpi .v { font-size:23px; font-weight:bold; color:#f1f5f9; }
.kpi .f { font-size:11px; color:#34d399; }
.kpi .f.warn { color:#fbbf24; }

/* summary + strengths */
.grid2 { display:grid; grid-template-columns:1.3fr 1fr; gap:16px; margin-bottom:16px; }
.ai-sum { background:linear-gradient(135deg,#0d2118,#0d1c2e); border:1px solid #1d4d38; }
.ai-sum h3 .ic { color:#34d399; }
.ai-sum p { font-size:13.5px; color:#b8cdbe; text-align:justify; }
.ai-sum .conf { font-size:11.5px; color:#5d7691; margin-top:8px; }

.sw { display:grid; gap:12px; }
.sw-box { border-radius:14px; padding:14px 16px; }
.sw-box.up { background:#0c2018; border:1px solid #1d4d38; }
.sw-box.down { background:#231318; border:1px solid #5f2330; }
.sw-box h4 { font-size:13.5px; margin-bottom:8px; }
.sw-box.up h4 { color:#6ee7b7; } .sw-box.down h4 { color:#fda4af; }
.chips { display:flex; flex-wrap:wrap; gap:7px; }
.chip { font-size:12px; border-radius:16px; padding:3px 12px; }
.sw-box.up .chip { background:rgba(16,185,129,.14); border:1px solid rgba(16,185,129,.4); color:#a7f3d0; }
.sw-box.down .chip { background:rgba(244,63,94,.12); border:1px solid rgba(244,63,94,.4); color:#fecdd3; }

/* charts row */
.charts { display:grid; grid-template-columns:1fr 1.2fr; gap:16px; margin-bottom:16px; }
.radar-wrap { display:flex; justify-content:center; }
.legend { display:flex; flex-direction:column; gap:8px; font-size:12.5px; }
.legend .li { display:flex; justify-content:space-between; align-items:center; background:#0a1727; border:1px solid #1a2f47; border-radius:9px; padding:7px 12px; }
.legend .li .nm { color:#cbd5e1; }
.bar { width:110px; height:7px; background:#16293f; border-radius:5px; overflow:hidden; display:inline-block; vertical-align:middle; margin:0 8px; }
.bar i { display:block; height:100%; border-radius:5px; }
.legend .pc { color:#8ea9c4; font-size:11.5px; width:36px; text-align:left; }
.lv-tag { font-size:10.5px; border-radius:12px; padding:1px 8px; margin-inline-start:6px; }
.lv-e { background:rgba(16,185,129,.14); color:#6ee7b7; border:1px solid rgba(16,185,129,.4); }
.lv-m { background:rgba(56,189,248,.12); color:#7dd3fc; border:1px solid rgba(56,189,248,.4); }
.lv-h { background:rgba(244,114,182,.12); color:#f9a8d4; border:1px solid rgba(244,114,182,.4); }

/* pie pies */
.pies { display:flex; gap:18px; justify-content:space-around; align-items:center; flex-wrap:wrap; }
.pie { text-align:center; }
.pie .circle { width:96px; height:96px; border-radius:50%; position:relative; margin:0 auto 6px; }
.pie .circle::after { content:''; position:absolute; inset:14px; border-radius:50%; background:#0d1c2e; }
.pie .circle b { position:absolute; inset:0; display:flex; align-items:center; justify-content:center; font-size:17px; color:#f1f5f9; z-index:2; }
.pie .cap { font-size:12px; color:#8ea9c4; }
.pie .cap small { display:block; color:#5d7691; font-size:10.5px; }

/* questions */
.qsec { margin-bottom:16px; }
.qsec-head { display:flex; justify-content:space-between; align-items:center; margin-bottom:12px; }
.qsec-head h2 { font-size:17px; color:#e8f1f8; }
.qsec-head .hint { font-size:12px; color:#5d7691; }
.q { background:#0d1c2e; border:1px solid #1d3548; border-radius:14px; margin-bottom:11px; overflow:hidden; }
.q-head { display:flex; justify-content:space-between; gap:14px; align-items:center; padding:14px 18px; cursor:pointer; }
.q-head:hover { background:#102338; }
.q-meta { display:flex; gap:8px; align-items:center; flex-wrap:wrap; font-size:11.5px; color:#5d7691; }
.q-no { color:#10b981; font-weight:700; font-size:12.5px; }
.q-text { color:#dbe7f5; font-size:13.5px; margin-top:4px; }
.q-score { display:flex; align-items:center; gap:10px; white-space:nowrap; }
.score-pill { font-weight:bold; font-size:14px; border-radius:16px; padding:3px 14px; }
.s5 { background:rgba(16,185,129,.16); color:#34d399; border:1px solid rgba(16,185,129,.5); }
.s4 { background:rgba(56,189,248,.14); color:#7dd3fc; border:1px solid rgba(56,189,248,.45); }
.s3 { background:rgba(251,191,36,.14); color:#fcd34d; border:1px solid rgba(251,191,36,.45); }
.s2 { background:rgba(249,115,22,.14); color:#fdba74; border:1px solid rgba(249,115,22,.45); }
.s1 { background:rgba(244,63,94,.14); color:#fb7185; border:1px solid rgba(244,63,94,.45); }
.chev { color:#5d7691; transition:.2s; }
.q.open .chev { transform:rotate(180deg); }
.q-body { display:none; padding:0 18px 16px; border-top:1px dashed #1a2f47; }
.q.open .q-body { display:block; }
.q-body .lbl { font-size:11.5px; color:#5d7691; margin-top:12px; margin-bottom:3px; }
.answer { background:#0a1727; border:1px solid #1a2f47; border-right:3px solid #27486b; border-radius:9px; padding:10px 13px; font-size:13px; color:#a9bdd4; }
.fb { font-size:13px; color:#c7d8ea; background:#0b1e33; border:1px solid #1f3b56; border-radius:9px; padding:10px 13px; }
.fb .good { color:#6ee7b7; } .fb .bad { color:#fda4af; }
.q-tools { display:flex; gap:10px; margin-top:12px; align-items:center; flex-wrap:wrap; }
.ts { font-size:11.5px; color:#5d7691; background:#0a1727; border:1px solid #1a2f47; border-radius:14px; padding:2px 10px; direction:ltr; }

/* recommendation */
.rec { display:grid; grid-template-columns:1fr 1fr; gap:16px; margin-bottom:16px; }
.rec-main { background:linear-gradient(135deg,#0d2118,#0d1c2e); border:1px solid #1d4d38; }
.flags .fl { display:flex; gap:10px; align-items:flex-start; background:#231318; border:1px solid #5f2330; border-radius:10px; padding:10px 13px; margin-bottom:8px; font-size:12.5px; color:#fecdd3; }
.flags .fl.ok { background:#0c2018; border-color:#1d4d38; color:#a7f3d0; }
.rec-actions { display:flex; gap:10px; margin-top:14px; flex-wrap:wrap; }

/* dev notes */
.notes { display:grid; grid-template-columns:1fr 1fr; gap:16px; margin-top:26px; }
.note-card { background:#0d1c2e; border:1px solid #1d3548; border-radius:16px; padding:18px 20px; }
.note-card h3 { font-size:15px; color:#5eead4; margin-bottom:8px; }
.note-card.be h3 { color:#fbbf24; }
.note-card.pr h3 { color:#c4b5fd; }
.note-card ul { margin-right:18px; font-size:12.8px; color:#9db3ca; }
.note-card li { margin-bottom:6px; }
.note-card pre {
    background:#06111c; border:1px solid #1f3b56; border-radius:10px; padding:14px;
    font-size:11.5px; color:#93c5fd; direction:ltr; text-align:left; overflow-x:auto;
    line-height:1.7; font-family:ui-monospace,Consolas,monospace; white-space:pre-wrap;
}
.full { grid-column:1/-1; }
@media (max-width:900px){
    .hero,.kpis,.grid2,.charts,.rec,.notes { grid-template-columns:1fr; }
}

""";
}
