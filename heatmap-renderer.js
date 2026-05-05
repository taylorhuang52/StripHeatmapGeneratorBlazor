/**
 * heatmap-renderer.js
 * 由 Blazor JS Interop 呼叫，使用 Canvas 2D API 繪製 Heatmap。
 * 放置路徑：wwwroot/heatmap-renderer.js
 */

/**
 * 根據 C# 傳入的 JSON payload 繪製 heatmap，回傳 base64 PNG 字串。
 * @param {string} payloadJson - StripRendererService.BuildPayload() 的輸出
 * @returns {string} base64 PNG（不含 data:image/png;base64, 前綴）
 */
window.renderHeatmap = function (payloadJson) {
    const p = JSON.parse(payloadJson);

    const canvas = document.createElement('canvas');
    canvas.width  = p.width;
    canvas.height = p.height;
    const ctx = canvas.getContext('2d');

    // 白底
    ctx.fillStyle = '#ffffff';
    ctx.fillRect(0, 0, p.width, p.height);

    // Title
    ctx.fillStyle   = p.titleColor;
    ctx.font        = 'bold 18px Arial';
    ctx.textBaseline = 'alphabetic';
    const tw = ctx.measureText(p.title).width;
    ctx.fillText(p.title, (p.width - tw) / 2, 40);

    // Cells & labels
    for (const c of p.cells) {
        if (c.type === 'label') {
            ctx.fillStyle    = '#000000';
            ctx.font         = 'bold 11px Arial';
            ctx.textBaseline = 'alphabetic';
            ctx.fillText(c.text, c.x, c.y);
        } else {
            // 填色
            ctx.fillStyle = c.bg;
            ctx.fillRect(c.x, c.y, c.w, c.h);

            // 數值文字
            if (c.text) {
                ctx.fillStyle    = c.fg;
                ctx.font         = `${p.fontSize}px Arial`;
                ctx.textBaseline = 'middle';
                const vw = ctx.measureText(c.text).width;
                const tx = c.x + (c.w - vw) / 2;
                const ty = c.y + c.h / 2;
                ctx.fillText(c.text, tx, ty);
            }
        }
    }

    // 回傳 base64（去掉 data:image/png;base64, 前綴）
    return canvas.toDataURL('image/png').split(',')[1];
};

/**
 * 觸發瀏覽器下載 PNG 檔案。
 * @param {string} fileName
 * @param {string} base64
 */
window.downloadBase64 = function (fileName, base64) {
    const a    = document.createElement('a');
    a.href     = 'data:image/png;base64,' + base64;
    a.download = fileName;
    a.click();
};
