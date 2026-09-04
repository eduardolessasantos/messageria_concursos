// ==============================================================================
// ConcursosTI - Portal GitHub Pages
// Gerenciador de Inicialização, Polling de Dependências e Dashboard
// ==============================================================================

const STORAGE_KEY = "concursos_api_url";
const DEFAULT_URL = window.location.hostname === "localhost" || window.location.hostname === "127.0.0.1"
  ? "http://localhost:5000"
  : "https://concursos-api.onrender.com"; // URL padrão de produção no Render

let apiUrl = localStorage.getItem(STORAGE_KEY) || DEFAULT_URL;
let timerSeconds = 0;
let timerInterval = null;
let pollingActive = true;

const dom = {
  loadingScreen: document.getElementById("loading-screen"),
  mainDashboard: document.getElementById("main-dashboard"),
  timerDisplay: document.getElementById("timer-display"),
  apiUrlInput: document.getElementById("api-url-input"),
  btnSaveUrl: document.getElementById("btn-save-url"),
  btnRetryNow: document.getElementById("btn-retry-now"),
  statusApi: document.getElementById("status-api"),
  statusDb: document.getElementById("status-db"),
  statusBroker: document.getElementById("status-broker"),
  statusWorkers: document.getElementById("status-workers"),
  totalConcursos: document.getElementById("metric-total-concursos"),
  metricApiStatus: document.getElementById("metric-api-status"),
  metricBrokerStatus: document.getElementById("metric-broker-status"),
  metricDbStatus: document.getElementById("metric-db-status"),
  tableBody: document.getElementById("concursos-table-body"),
  searchInput: document.getElementById("search-input"),
  btnColetar: document.getElementById("btn-coletar"),
  btnTestEmail: document.getElementById("btn-test-email"),
  linkSwagger: document.getElementById("link-swagger"),
  linkHealth: document.getElementById("link-health"),
  toast: document.getElementById("toast-msg")
};

let listaConcursos = [];

// Inicialização
document.addEventListener("DOMContentLoaded", () => {
  dom.apiUrlInput.value = apiUrl;
  atualizarLinksExteriores();
  iniciarTimer();
  verificarSaudeTodosProjetos();

  // Eventos de interface
  dom.btnSaveUrl.addEventListener("click", () => {
    const nova = dom.apiUrlInput.value.trim().replace(/\/+$/, "");
    if (nova) {
      apiUrl = nova;
      localStorage.setItem(STORAGE_KEY, apiUrl);
      showToast("URL da API atualizada para: " + apiUrl);
      atualizarLinksExteriores();
      timerSeconds = 0;
      verificarSaudeTodosProjetos();
    }
  });

  dom.btnRetryNow.addEventListener("click", () => {
    verificarSaudeTodosProjetos();
  });

  dom.searchInput.addEventListener("input", (e) => {
    filtrarTabela(e.target.value);
  });

  dom.btnColetar.addEventListener("click", async () => {
    dom.btnColetar.disabled = true;
    dom.btnColetar.innerText = "⏳ Coletando Editais de TI...";
    try {
      const res = await fetch(`${apiUrl}/api/concursos/coletar`, { method: "POST" });
      const data = await res.json();
      showToast(data.mensagem || "Coleta realizada com sucesso!");
      await carregarConcursos();
    } catch (err) {
      showToast("Erro ao acionar coleta: " + err.message);
    } finally {
      dom.btnColetar.disabled = false;
      dom.btnColetar.innerText = "⚡ Executar Coleta Imediata";
    }
  });

  dom.btnTestEmail.addEventListener("click", async () => {
    dom.btnTestEmail.disabled = true;
    dom.btnTestEmail.innerText = "⏳ Disparando Notificação...";
    try {
      const res = await fetch(`${apiUrl}/api/concursos/test-email?orgao=Dataprev&cargo=Analista+de+TI`, { method: "POST" });
      const data = await res.json();
      showToast(data.mensagem || "Evento de teste disparado com sucesso!");
    } catch (err) {
      showToast("Erro no disparo de teste: " + err.message);
    } finally {
      dom.btnTestEmail.disabled = false;
      dom.btnTestEmail.innerText = "✉️ Disparar E-mail Teste";
    }
  });
});

function atualizarLinksExteriores() {
  dom.linkSwagger.href = `${apiUrl}/`;
  dom.linkHealth.href = `${apiUrl}/health`;
}

function iniciarTimer() {
  if (timerInterval) clearInterval(timerInterval);
  timerInterval = setInterval(() => {
    timerSeconds++;
    const min = String(Math.floor(timerSeconds / 60)).padStart(2, '0');
    const sec = String(timerSeconds % 60).padStart(2, '0');
    dom.timerDisplay.innerText = `Tempo de espera: ${min}:${sec}s (Render gratuito pode levar até 50s no primeiro acesso)`;
  }, 1000);
}

// Polling de verificação de saúde e prontidão de todos os projetos
async function verificarSaudeTodosProjetos() {
  if (!pollingActive) return;

  try {
    const controller = new AbortController();
    const timeoutId = setTimeout(() => controller.abort(), 6000);

    const response = await fetch(`${apiUrl}/health`, {
      method: "GET",
      signal: controller.signal,
      headers: { "Accept": "application/json" }
    });
    clearTimeout(timeoutId);

    if (response.ok) {
      const data = await response.json();
      
      // Avalia dependências
      const isApiOk = data.status === "Healthy" || data.status === "Degraded";
      const isDbOk = data.dependencies?.database === "Connected" || data.status === "Healthy";
      const isBrokerOk = data.dependencies?.broker === "Configured";

      if (isApiOk) marcarStatus(dom.statusApi, "Pronto", "ready");
      if (isDbOk) marcarStatus(dom.statusDb, "Conectado", "ready");
      if (isBrokerOk) marcarStatus(dom.statusBroker, "Ativo", "ready");
      marcarStatus(dom.statusWorkers, "Operacional", "ready");

      // Atualiza métricas
      dom.metricApiStatus.innerText = "Online";
      dom.metricApiStatus.style.color = "var(--success)";
      dom.metricDbStatus.innerText = isDbOk ? "MySQL Conectado" : "Aguardando";
      dom.metricBrokerStatus.innerText = "RabbitMQ Ativo";

      // TODOS OS PROJETOS PRONTOS -> LIBERA A NAVEGAÇÃO!
      liberarNavegacao();
      await carregarConcursos();
      return;
    } else {
      marcarStatus(dom.statusApi, "Iniciando...", "waiting");
    }
  } catch (ex) {
    marcarStatus(dom.statusApi, "Aguardando spin-up...", "waiting");
    marcarStatus(dom.statusDb, "Aguardando API...", "waiting");
    marcarStatus(dom.statusBroker, "Aguardando...", "waiting");
  }

  // Se não estiver pronto, agenda nova checagem a cada 2.5 segundos
  setTimeout(verificarSaudeTodosProjetos, 2500);
}

function marcarStatus(elem, texto, classe) {
  elem.innerText = texto;
  elem.className = `status-badge ${classe}`;
}

// Libera a página de navegação e esconde o GIF de carregamento
function liberarNavegacao() {
  pollingActive = false;
  if (timerInterval) clearInterval(timerInterval);

  dom.loadingScreen.classList.add("hidden");
  dom.mainDashboard.classList.add("active");
  showToast("Todos os projetos foram carregados! Painel liberado.");
}

async function carregarConcursos() {
  try {
    const res = await fetch(`${apiUrl}/api/concursos`);
    if (res.ok) {
      listaConcursos = await res.json();
      dom.totalConcursos.innerText = listaConcursos.length;
      renderizarTabela(listaConcursos);
    }
  } catch (err) {
    console.warn("Falha ao carregar concursos:", err);
  }
}

function renderizarTabela(itens) {
  dom.tableBody.innerHTML = "";
  if (!itens || itens.length === 0) {
    dom.tableBody.innerHTML = `<tr><td colspan="6" style="text-align:center; color:var(--text-muted); padding:30px;">Nenhum edital de TI encontrado no momento. Execute a coleta acima!</td></tr>`;
    return;
  }

  itens.forEach(c => {
    const tr = document.createElement("tr");
    const dataCaptura = new Date(c.dataCaptura).toLocaleDateString("pt-BR", { hour: '2-digit', minute: '2-digit' });

    tr.innerHTML = `
      <td><strong>${c.orgao}</strong></td>
      <td><span class="tag-score">${c.cargo}</span></td>
      <td><span class="tag-salario">${c.salario}</span></td>
      <td>${c.fonte || 'PciConcursos'}</td>
      <td>${dataCaptura}</td>
      <td style="text-align: center;">
        <a href="${c.link}" target="_blank" class="link-btn">Abrir Edital ↗</a>
      </td>
    `;
    dom.tableBody.appendChild(tr);
  });
}

function filtrarTabela(termo) {
  const busca = termo.toLowerCase().trim();
  if (!busca) {
    renderizarTabela(listaConcursos);
    return;
  }

  const filtrados = listaConcursos.filter(c => 
    c.cargo?.toLowerCase().includes(busca) ||
    c.orgao?.toLowerCase().includes(busca) ||
    c.salario?.toLowerCase().includes(busca)
  );

  renderizarTabela(filtrados);
}

function showToast(msg) {
  dom.toast.innerText = msg;
  dom.toast.style.display = "block";
  setTimeout(() => {
    dom.toast.style.display = "none";
  }, 4000);
}
