namespace SupraAutoPackLe;

/// <summary>Read-only, WMS-origin DOM extraction. Returns only watched SKU matches.
/// No fetch/XHR, credential access, MutationObserver, or WMS modifications.</summary>
internal static class WmsSkuJs
{
    internal const string Script = """
    (() => {
      'use strict';
      if(window.__supraSkuAuditV1) return;
      const compact = value => String(value ?? '').replace(/\s+/g,' ').trim();
      const norm = value => compact(value).normalize('NFC').toLocaleLowerCase('vi');
      const visible = el => !!el && el.isConnected && el.getClientRects().length > 0 &&
        getComputedStyle(el).display !== 'none' && getComputedStyle(el).visibility !== 'hidden';
      const one = items => items.length === 1 ? items[0] : null;
      const validPath = so => /^[A-Z0-9_-]{5,50}$/.test(so) &&
        location.protocol === 'https:' && location.hostname === 'wms-supra.winmart.vn' &&
        location.pathname.replace(/\/$/,'') === '/sft3/app/saleorder/details/' + so &&
        location.search === '?PickType=PickOrder';
      const tabs = () => [...document.querySelectorAll('[role="tab"],.mat-tab-label')]
        .filter(x => visible(x) && norm(x.innerText) === 'thông tin chính');
      const selected = tab => tab && (tab.getAttribute('aria-selected') === 'true' ||
        tab.classList.contains('mat-tab-label-active'));
      const tableForSku = () => {
        const tables=[...document.querySelectorAll('mat-table')].filter(visible);
        return one(tables.filter(table => [...table.querySelectorAll('mat-header-cell')]
          .some(c => norm(c.innerText) === 'sku')));
      };
      const pagination = () => {
        const paginator=one([...document.querySelectorAll('mat-paginator')].filter(visible));
        if(!paginator) return null;
        const label=compact(paginator.querySelector('.mat-paginator-range-label')?.textContent);
        const m=label.match(/(\d+)\s*[-–—]\s*(\d+)\s*(?:of|trên|\/)\s*(\d+)/i);
        const select=one([...paginator.querySelectorAll('mat-select')].filter(visible));
        const pageSize=Number(compact(select?.innerText).match(/\b(\d+)\b/)?.[1] || 0);
        return {paginator, label, start:m?Number(m[1]):0,
          end:m?Number(m[2]):0,total:m?Number(m[3]):0,pageSize,select};
      };
      const headers = table => [...table.querySelectorAll('mat-header-cell')].map(x=>norm(x.innerText));
      const onlyColumn = (cols, names) => {
        const matches=cols.map((x,i)=>names.includes(x)?i:-1).filter(i=>i>=0);
        return matches.length===1?matches[0]:-1;
      };
      const peek = (so, watched) => {
        if(!validPath(so)) return {ok:false,reason:'WRONG_SO_PAGE'};
        const tab=one(tabs());
        if(!tab) return {ok:false,reason:'MAIN_TAB_NOT_READY'};
        if(!selected(tab)) return {ok:false,reason:'MAIN_TAB_NOT_SELECTED'};
        const table=tableForSku();
        if(!table) return {ok:false,reason:'SKU_TABLE_NOT_READY'};
        const cols=headers(table);
        const idx=[
          onlyColumn(cols,['sku']),
          onlyColumn(cols,['tên sản phẩm','tên hàng']),
          onlyColumn(cols,['pcb']),
          onlyColumn(cols,['sl đặt','số lượng đặt','s.l đặt']),
          onlyColumn(cols,['base unit','đơn vị cơ sở'])
        ];
        if(idx.some(i=>i<0)) return {ok:false,reason:'SKU_COLUMNS_CHANGED'};
        const pg=pagination();
        if(!pg || !pg.select) return {ok:false,reason:'PAGINATOR_NOT_READY'};
        if(pg.total<0 || pg.start<0 || pg.end<0 ||
           (pg.total>0 && (pg.end<pg.start || pg.end>pg.total)))
          return {ok:false,reason:'PAGINATION_INVALID'};
        const rows=[...table.querySelectorAll('mat-row')].filter(visible);
        if(pg.total>0 && rows.length===0)
          return {ok:false,reason:'SKU_ROWS_LOADING'};
        const tracked=new Set((watched||[]).map(x=>compact(x).toUpperCase()));
        const matches=[];
        for(const row of rows) {
          const cells=[...row.querySelectorAll('mat-cell')];
          if(cells.length<cols.length) return {ok:false,reason:'SKU_ROW_INCOMPLETE'};
          const values=idx.map(i=>compact(cells[i].innerText||cells[i].textContent));
          if(!values[0]) return {ok:false,reason:'SKU_CELL_EMPTY'};
          if(tracked.has(values[0].toUpperCase()))
            matches.push({sku:values[0],productName:values[1],
              pcb:values[2],orderedQuantity:values[3],baseUnit:values[4]});
        }
        return {ok:true,reason:'OK',matches,rows:rows.length,
          start:pg.start,end:pg.end,total:pg.total,pageSize:pg.pageSize,
          last:pg.total===0 || pg.end>=pg.total};
      };
      const activate = so => {
        if(!validPath(so)) return {ok:false,reason:'WRONG_SO_PAGE'};
        const tab=one(tabs());
        if(!tab) return {ok:false,reason:'MAIN_TAB_NOT_READY'};
        if(selected(tab)) return {ok:true,reason:'ALREADY_ACTIVE'};
        if(!visible(tab) || tab.getAttribute('aria-disabled')==='true')
          return {ok:false,reason:'TAB_DISABLED'};
        tab.click();
        return {ok:true,reason:'ACTIVATED'};
      };
      const page100 = so => {
        if(!validPath(so))return {ok:false,reason:'WRONG_SO_PAGE'};
        const pg=pagination();
        if(!pg || !pg.select)return {ok:false,reason:'PAGINATOR_NOT_READY'};
        if(pg.pageSize===100)return {ok:true,reason:'PAGE_100'};
        const options=[...document.querySelectorAll('mat-option,[role="option"]')]
          .filter(x=>visible(x) && compact(x.innerText)==='100');
        if(options.length===1){options[0].click();return {ok:true,reason:'SELECTED_100'};}
        if(options.length>1)return {ok:false,reason:'OPTION_AMBIGUOUS'};
        pg.select.click();
        return {ok:true,reason:'OPEN_100_OPTIONS'};
      };
      const next = so => {
        if(!validPath(so))return {ok:false,reason:'WRONG_SO_PAGE'};
        const pg=pagination();
        if(!pg || pg.total===0 || pg.end>=pg.total)
          return {ok:false,reason:'NO_NEXT_PAGE'};
        const buttons=[...pg.paginator.querySelectorAll('button.mat-paginator-navigation-next')].filter(
          x=>visible(x) && !x.disabled && x.getAttribute('aria-disabled')!=='true');
        const button=one(buttons);
        if(!button)return {ok:false,reason:'NEXT_NOT_UNIQUE'};
        button.click();return {ok:true,reason:'PAGE_ADVANCED'};
      };
      window.__supraSkuAuditV1={peek,activate,page100,next};
    })();
    """;
}
