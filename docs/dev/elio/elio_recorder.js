// Elio recorder v1.3: лог move / skip / support / displace / pass + replay.
// Зависит от window.ElioEngine v1.3.
(function(){
  const E = window.ElioEngine;
  let moves=[];      // массив записей хода
  let replayIndex=0;
  let replayState=null;

  function reset(){ moves=[]; replayIndex=0; replayState=null; }
  function addMove(record){ if(record) moves.push(record); }
  function getMoves(){ return moves.slice(); }

  function exportText(){
    const lines=[];
    lines.push('# Elio game log');
    lines.push('moves: '+moves.length);
    moves.forEach(m=>{
      const cap  = m.capture ? ` x${m.capture.pieceId}` : '';
      const push = kind(m)==='displace' ? ` → ${m.to&&m.to.q},${m.to&&m.to.r}` : '';
      const pass = m.passTo ? ` → передача: ${E.FACTION_NAME[m.passTo]}` : '';
      if(kind(m)==='skip') lines.push(`${m.moveNumber}. SKIP ${E.FACTION_NAME[m.faction]}${pass}`);
      else if(kind(m)==='support') lines.push(`${m.moveNumber}. Поддержка ${m.pieceId} → ${m.targetId}${pass}`);
      else if(kind(m)==='displace') lines.push(`${m.moveNumber}. Оттеснение ${m.pieceId}: ${m.from&&m.from.q},${m.from&&m.from.r} → ${m.to&&m.to.q},${m.to&&m.to.r}${pass}`)
      else lines.push(`${m.moveNumber}. ${m.pieceId} ${m.from.q},${m.from.r} → ${m.to.q},${m.to.r}${cap}${pass}`);
    });
    return lines.join('\n');
  }
  function kind(m){ return m.kind||'move'; }
  function exportJSON(){ return JSON.stringify({moves:moves}, null, 2); }

  function importJSON(jsonStr){
    const obj=JSON.parse(jsonStr);
    moves=obj.moves||[];
    replayIndex=0; replayState=null;
    return moves.length;
  }

  function startReplay(mode, death){
    replayState=E.init({turn:mode||'cycle', death:death||'main'});
    replayIndex=0; return replayState;
  }

  function replayOne(s,m){
    const k=kind(m);
    // фракция хода восстанавливается из записи для всех режимов
    s.turnFaction=m.faction;
    if(k==='skip') return E.skipTurn(s,m.passTo);
    if(k==='support') return E.applySupport(s,m.pieceId||m.zeroId, m.targetId, m.passTo);
    if(k==='displace'){ return E.applyDisplace(s, m.pieceId, (m.from&&m.from.q), (m.from&&m.from.r), m.passTo); }
    return E.applyMove(s, m.pieceId, m.to.q, m.to.r, m.passTo);
  }

  function stepReplay(){
    if(!replayState) replayState=E.init();
    if(replayIndex>=moves.length) return {state:replayState, done:true};
    const m=moves[replayIndex];
    const r=replayOne(replayState,m);
    if(r.error){
      return {state:replayState, done:false, error:r.error, index:replayIndex};
    }
    replayState=r.state; replayIndex++;
    return {state:replayState, done:replayIndex>=moves.length, index:replayIndex};
  }

  function replayTo(n){
    replayState=E.init(); replayIndex=0;
    while(replayIndex<n&&replayIndex<moves.length){
      const r=stepReplay(); if(r.error)break;
    }
    return replayState;
  }

  function getReplayIndex(){return replayIndex;}
  function getReplayState(){return replayState;}

  window.ElioRecorder={reset,addMove,getMoves,exportText,exportJSON,importJSON,
    startReplay,stepReplay,replayTo,getReplayIndex,getReplayState};
})();
