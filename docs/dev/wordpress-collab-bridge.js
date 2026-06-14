/* ============================================================================
 * WordPress ↔ roadmap.html bridge
 * ----------------------------------------------------------------------------
 * Вставить в functions.php дочерней темы (или в виджет "Произвольный HTML"
 * на странице с iframe, или просто в <script> страницы WordPress).
 *
 * Что делает:
 *   1) При загрузке страницы WordPress с #team в URL — шлёт в iframe команду
 *      открыть секцию коллаборации.
 *   2) Получает от iframe данные о позиции секции и скроллит родительское окно
 *      так, чтобы iframe оказался в зоне видимости с учётом позиции секции.
 *   3) Слушает hashchange на родительском URL — если поставили #team после
 *      загрузки, повторно шлёт команду.
 * ============================================================================ */
(function() {
  'use strict';

  function findIframe() {
    /* Ищем iframe с roadmap.html (любой путь в /project-c/roadmap.html) */
    var iframes = document.querySelectorAll('iframe');
    for (var i = 0; i < iframes.length; i++) {
      var src = iframes[i].src || '';
      if (src.indexOf('project-c/roadmap.html') !== -1 ||
          src.indexOf('project-c\\roadmap.html') !== -1) {
        return iframes[i];
      }
    }
    return null;
  }

  function tellIframeToOpen(iframe) {
    if (!iframe || !iframe.contentWindow) return;
    try {
      iframe.contentWindow.postMessage({ type: 'collab-open' }, '*');
    } catch(e) {}
  }

  function scrollParentToIframeSection(iframe, offsetTop, height) {
    if (!iframe) return;
    /* Позиция iframe в родительском документе */
    var iframeRect = iframe.getBoundingClientRect();
    var sectionAbsoluteY = iframeRect.top + window.scrollY + offsetTop;
    /* Скроллим родителя так, чтобы секция оказалась у верха родительского viewport */
    var targetY = sectionAbsoluteY - 80; /* 80px отступ от верха */
    window.scrollTo({ top: targetY, behavior: 'smooth' });
  }

  /* === Слушаем сообщения от iframe === */
  window.addEventListener('message', function(ev) {
    if (!ev.data || typeof ev.data !== 'object') return;

    /* iframe просит проскроллить родителя до своей секции */
    if (ev.data.type === 'collab-scroll') {
      var iframe = findIframe();
      scrollParentToIframeSection(iframe, ev.data.offsetTop || 0, ev.data.height || 0);
    }

    /* iframe сообщает об изменении хэша (на случай, если родитель хочет обновить URL) */
    if (ev.data.type === 'collab-hash' && ev.data.hash) {
      try {
        history.replaceState(null, '', ev.data.hash);
      } catch(e) {}
    }
  });

  /* === При загрузке: если URL уже содержит #team — открыть секцию === */
  function checkInitialHash() {
    if (location.hash === '#team') {
      var iframe = findIframe();
      if (iframe) {
        if (iframe.contentDocument && iframe.contentDocument.readyState === 'complete') {
          tellIframeToOpen(iframe);
        } else {
          iframe.addEventListener('load', function() {
            setTimeout(function() { tellIframeToOpen(iframe); }, 300);
          });
        }
      }
    }
  }

  /* === Если #team появился после загрузки — тоже открыть === */
  window.addEventListener('hashchange', function() {
    if (location.hash === '#team') {
      tellIframeToOpen(findIframe());
    }
  });

  /* === Старт === */
  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', checkInitialHash);
  } else {
    checkInitialHash();
  }
})();
