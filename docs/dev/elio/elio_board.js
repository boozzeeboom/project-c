// Elio board renderer + interaction
// Depends on window.ElioEngine
(function(){
  const E = window.ElioEngine;
  const NS='http://www.w3.org/2000/svg';
  let svg=null;
  let cellIndex=[]; // {q,r,kind,fieldId,el}
  let pieceLayer=null;
  let highlightLayer=null;
  let selectedPieceId=null;
  let legalTargets=[];
  let supportTargets=[];   // ids pieces, которые может «поддержать» выделенный нуль
  let state=null;
  let onMoveRequest=null; // (pieceId,q,r)=>void
  let onSupportRequest=null; // (zeroId,targetId)=>void

  function axialToPx(q,r){const S=40;const SQ3=Math.sqrt(3);const O={x:425,y:440};return {x:O.x+1.5*S*q, y:O.y+SQ3*S*(r+q/2)};}
  function hexPoints(cx,cy,size){const pts=[];for(let i=0;i<6;i++){const a=Math.PI/180*(60*i);pts.push((cx+size*Math.cos(a)).toFixed(1)+','+(cy+size*Math.sin(a)).toFixed(1));}return pts.join(' ');}

  function buildBoard(){
    svg.innerHTML='';
    cellIndex=[];
    // rebuild cells from engine geometry
    const S=40;
    // field cells
    E.FIELD_INFO.forEach(f=>{
      const cells=E.patch(f.center[0],f.center[1]);
      cells.forEach(c=>{
        const p=axialToPx(c[0],c[1]);
        const poly=document.createElementNS(NS,'polygon');
        poly.setAttribute('points',hexPoints(p.x,p.y,S-1));
        const isYellow=E.GATE_SET.has(c[0]+','+c[1]);
        if(isYellow){poly.setAttribute('fill','#c9a22733');poly.setAttribute('stroke','#c9a227');poly.setAttribute('stroke-width','1.5');}
        else{poly.setAttribute('fill',f.color+'22');poly.setAttribute('stroke',f.color+'66');poly.setAttribute('stroke-width','1');}
        poly.setAttribute('class','hexcell');
        poly.setAttribute('data-q',c[0]); poly.setAttribute('data-r',c[1]); poly.setAttribute('data-field',f.id); poly.setAttribute('data-kind','field');
        poly.addEventListener('click',()=>handleCellClick(f.id,c[0],c[1]));
        svg.appendChild(poly);
        cellIndex.push({q:c[0],r:c[1],kind:'field',fieldId:f.id,el:poly});
        const t=document.createElementNS(NS,'text');
        t.setAttribute('x',p.x);t.setAttribute('y',p.y+3);
        t.setAttribute('text-anchor','middle'); t.setAttribute('fill', isYellow?'#e8c84a':'#c8c8e0');
        t.setAttribute('font-size','8'); t.setAttribute('font-weight','bold'); t.textContent=E.codeMap.get(c[0]+','+c[1]);
        svg.appendChild(t);
      });
      // outline
      // skip outline for simplicity (optional)
    });
    // disputed
    E.DISPUTED.forEach(d=>{
      const p=axialToPx(d.q,d.r);
      const poly=document.createElementNS(NS,'polygon');
      poly.setAttribute('points',hexPoints(p.x,p.y,S-1));
      poly.setAttribute('fill','#8a8ab018'); poly.setAttribute('stroke','#8a8ab0'); poly.setAttribute('stroke-dasharray','5 4'); poly.setAttribute('stroke-width','1.2');
      poly.setAttribute('class','hexcell'); poly.setAttribute('data-q',d.q); poly.setAttribute('data-r',d.r); poly.setAttribute('data-kind','disputed');
      poly.addEventListener('click',()=>handleCellClick('disputed',d.q,d.r));
      svg.appendChild(poly);
      cellIndex.push({q:d.q,r:d.r,kind:'disputed',fieldId:'disputed',el:poly});
      const t=document.createElementNS(NS,'text');
      t.setAttribute('x',p.x);t.setAttribute('y',p.y+3); t.setAttribute('text-anchor','middle'); t.setAttribute('fill','#b0b0d0');
      t.setAttribute('font-size','9'); t.setAttribute('font-weight','bold'); t.textContent=d.code;
      svg.appendChild(t);
    });
    // center
    const cp=axialToPx(0,0);
    const cpoly=document.createElementNS(NS,'polygon');
    cpoly.setAttribute('points',hexPoints(cp.x,cp.y,S-1));
    cpoly.setAttribute('fill','#c9a22733'); cpoly.setAttribute('stroke','#c9a227'); cpoly.setAttribute('stroke-width','2');
    cpoly.setAttribute('class','hexcell'); cpoly.setAttribute('data-q',0); cpoly.setAttribute('data-r',0); cpoly.setAttribute('data-kind','center');
    cpoly.addEventListener('click',()=>handleCellClick('center',0,0));
    svg.appendChild(cpoly);
    cellIndex.push({q:0,r:0,kind:'center',fieldId:'center',el:cpoly});
    const ctxt=document.createElementNS(NS,'text');
    ctxt.setAttribute('x',cp.x); ctxt.setAttribute('y',cp.y-4); ctxt.setAttribute('text-anchor','middle'); ctxt.setAttribute('fill','#e8c84a');
    ctxt.setAttribute('font-size','14'); ctxt.setAttribute('font-weight','bold'); ctxt.textContent='Ц';
    svg.appendChild(ctxt);

    // field labels
    E.FIELD_INFO.forEach(f=>{
      const fc=f.center;
      const p=axialToPx(fc[0],fc[1]);
      const S=40;
      const label=document.createElementNS(NS,'text');
      label.setAttribute('x',p.x);
      if(f.id==='plus') label.setAttribute('y', p.y-5*S);
      else label.setAttribute('y', p.y+5*S);
      label.setAttribute('text-anchor','middle'); label.setAttribute('fill',f.color);
      label.setAttribute('font-size','15'); label.setAttribute('font-weight','bold'); label.setAttribute('letter-spacing','2');
      label.textContent=f.name;
      svg.appendChild(label);
    });
    // fix plus label higher
    // (already)

    pieceLayer=document.createElementNS(NS,'g');
    highlightLayer=document.createElementNS(NS,'g');
    svg.appendChild(highlightLayer);
    svg.appendChild(pieceLayer);
  }

  function handleCellClick(fieldId,q,r){
    const cell=cellIndex.find(c=>c.q===q && c.r===r);
    if(!cell) return;
    const p=E.pieceAt(state,q,r);
    if(selectedPieceId){
      const sel=state.pieces.find(x=>x.id===selectedPieceId);
      const mv=legalTargets.find(t=>t.q===q && t.r===r);
      if(mv){
        const pid=selectedPieceId;
        clearSelection();
        if(onMoveRequest) onMoveRequest(pid,q,r);
        return;
      }
      // поддержка: если выделен нуль и кликнули соседнюю фигуру, которую он может прикрыть
      if(sel&&E.isNullType(sel.type)&&p&&p.id!==sel.id&&supportTargets.includes(p.id)){
        clearSelection();
        if(onSupportRequest) onSupportRequest(sel.id, p.id);
        return;
      }
    }
    // select own piece of current turn
    if(p && p.faction===state.turnFaction && !state.gameOver && !p.inert){
      selectedPieceId=p.id;
      legalTargets=E.legalMoves(state,p.id);
      supportTargets = E.isNullType(p.type) ? E.legalSupportTargets(state,p.id) : [];
      highlightMoves();
      highlightSelected(cell.el);
    } else {
      clearSelection();
    }
    // info
    const info=document.getElementById('info');
    if(info){
      const code=E.codeMap.get(q+','+r);
      let extra='';
      if(p){
        extra=`<b>Фигура:</b> ${p.id} (${p.faction})`;
        if(E.isNullType(p.type)){
          extra+= p.supportTarget ? '<br>заявленный: щит → '+p.supportTarget : '<br>нейтральный';
        }
        if(p.inert) extra+=' <b>[инертная]</b>';
      }
      info.innerHTML=`<b>Клетка:</b> ${code||''}<br>q=${q}, r=${r}<br>${p?extra:'пусто'}`;
    }
  }

  function highlightMoves(){
    highlightLayer.innerHTML='';
    legalTargets.forEach(t=>{
      const p=axialToPx(t.q,t.r);
      const c=document.createElementNS(NS,'circle');
      c.setAttribute('cx',p.x); c.setAttribute('cy',p.y); c.setAttribute('r',12);
      const isDispl=!!t.displace;
      c.setAttribute('fill', t.capture?'#ff444455':(isDispl?'#4d9fff55':'#ffffff33'));
      c.setAttribute('stroke', t.capture?'#ff4444':(isDispl?'#4d9fff':'#ffffff'));
      c.setAttribute('stroke-width','2');
      c.setAttribute('pointer-events','none');
      highlightLayer.appendChild(c);
    });
    supportTargets.forEach(id=>{
      const tp=state.pieces.find(x=>x.id===id);
      if(!tp)return;
      const pos=axialToPx(tp.q,tp.r);
      const c=document.createElementNS(NS,'circle');
      c.setAttribute('cx',pos.x); c.setAttribute('cy',pos.y); c.setAttribute('r',9);
      c.setAttribute('fill','#c9a227aa');
      c.setAttribute('stroke','#c9a227');
      c.setAttribute('stroke-width','2');
      c.setAttribute('pointer-events','none');
      highlightLayer.appendChild(c);
    });
  }
  function highlightSelected(el){
    cellIndex.forEach(c=>c.el.classList.remove('cellsel'));
    if(el) el.classList.add('cellsel');
  }
  function clearSelection(){
    selectedPieceId=null; legalTargets=[]; supportTargets=[];
    if(highlightLayer) highlightLayer.innerHTML='';
    cellIndex.forEach(c=>c.el.classList.remove('cellsel'));
  }
  function highlightSelected(el){
    cellIndex.forEach(c=>c.el.classList.remove('cellsel'));
    if(el) el.classList.add('cellsel');
  }
  function clearSelection(){
    selectedPieceId=null; legalTargets=[];
    if(highlightLayer) highlightLayer.innerHTML='';
    cellIndex.forEach(c=>c.el.classList.remove('cellsel'));
  }

  function renderPieces(){
    if(!pieceLayer) return;
    pieceLayer.innerHTML='';
    // Линии поддержки (нуль → его цель)
    state.pieces.filter(p=>p.alive&&p.supportTarget).forEach(p=>{
      const t=state.pieces.find(x=>x.id===p.supportTarget&&x.alive);
      if(!t)return;
      const a=axialToPx(p.q,p.r), b=axialToPx(t.q,t.r);
      const line=document.createElementNS(NS,'line');
      line.setAttribute('x1',a.x);line.setAttribute('y1',a.y);
      line.setAttribute('x2',b.x);line.setAttribute('y2',b.y);
      line.setAttribute('stroke','#c9a227'); line.setAttribute('stroke-width','2'); line.setAttribute('opacity','.7');
      pieceLayer.appendChild(line);
    });
    state.pieces.filter(p=>p.alive).forEach(p=>{
      const pos=axialToPx(p.q,p.r);
      const g=document.createElementNS(NS,'g');
      const circle=document.createElementNS(NS,'circle');
      circle.setAttribute('cx',pos.x); circle.setAttribute('cy',pos.y); circle.setAttribute('r',13);
      const color=E.FACTION_COLOR[p.faction];
      circle.setAttribute('fill', color);
      // нейтральный нуль — пунктирная рамка; заявленный — сплошная толстая
      const isZ=E.isNullType(p.type);
      if(isZ){
        if(p.supportTarget){ circle.setAttribute('stroke','#ffffff'); circle.setAttribute('stroke-width','2.5'); }
        else { circle.setAttribute('stroke','#ffffff'); circle.setAttribute('stroke-width','1.5'); circle.setAttribute('stroke-dasharray','4 3'); }
      } else {
        // военные: маркер «короля»
        if(p.type==='line'){ circle.setAttribute('stroke','#c9a227'); circle.setAttribute('stroke-width','3'); }
        else { circle.setAttribute('stroke','#fff'); circle.setAttribute('stroke-width','1.5'); }
      }
      g.appendChild(circle);
      const txt=document.createElementNS(NS,'text');
      txt.setAttribute('x',pos.x); txt.setAttribute('y',pos.y+4);
      txt.setAttribute('text-anchor','middle'); txt.setAttribute('fill','#fff');
      txt.setAttribute('font-size','10'); txt.setAttribute('font-weight','bold');
      const codeMap = {line:'Л',heavy:'ТК',frigate:'Ф',corvette:'Кв',destroyer:'Мн',parliament:'КЛ',free:'Св',courier:'Ку',engineer:'Ин',saboteur:'Ди'};
      txt.textContent = codeMap[p.type] || p.type[0].toUpperCase();
      g.appendChild(txt);
      pieceLayer.appendChild(g);
    });
  }

  function render(s){
    state=s;
    renderPieces();
    // turn indicator handled by main
  }

  window.ElioBoard = {
    init: function(svgEl, moveCallback, supportCallback){
      svg=svgEl;
      onMoveRequest=moveCallback;
      onSupportRequest=supportCallback||null;
      buildBoard();
    },
    render: render,
    clearSelection: clearSelection,
    getState: ()=>state,
  };
})();
