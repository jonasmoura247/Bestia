(function () {
  'use strict';

  // Preferência "reduzir animações": guardada só neste navegador.
  function lerPreferencia(nome) {
    try { return window.localStorage.getItem('bestia.' + nome) === '1'; } catch (e) { return false; }
  }
  function gravarPreferencia(nome, valor) {
    try { window.localStorage.setItem('bestia.' + nome, valor ? '1' : '0'); } catch (e) { /* sem armazenamento */ }
  }

  document.body.classList.toggle('reduzir-animacoes', lerPreferencia('reduzir-animacoes'));

  document.addEventListener('change', function (evento) {
    var alvo = evento.target;
    if (alvo.matches('[data-auto-enviar]')) {
      alvo.form.submit();
    }
    if (alvo.matches('[data-preferencia]')) {
      gravarPreferencia(alvo.dataset.preferencia, alvo.checked);
      document.body.classList.toggle('reduzir-animacoes', alvo.checked);
    }
  });

  function definirMenu(aberto) {
    var menu = document.getElementById('menu-lateral');
    if (!menu) { return; }
    menu.classList.toggle('aberto', aberto);
    document.body.classList.toggle('menu-aberto', aberto);
  }

  document.addEventListener('click', function (evento) {
    var menu = document.getElementById('menu-lateral');
    if (evento.target.closest('[data-alternar-menu]')) {
      definirMenu(!(menu && menu.classList.contains('aberto')));
    } else if (menu && menu.classList.contains('aberto') && !evento.target.closest('#menu-lateral')) {
      // Tocou fora do menu aberto: fecha.
      definirMenu(false);
    }
  });

  document.addEventListener('keydown', function (evento) {
    if (evento.key === 'Escape') { definirMenu(false); }
  });

  document.querySelectorAll('[data-preferencia]').forEach(function (caixa) {
    caixa.checked = lerPreferencia(caixa.dataset.preferencia);
  });
})();
