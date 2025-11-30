// Squirrel Wiki v1.0 - Site JavaScript

/**
 * Toast Notification System
 * Provides consistent, non-intrusive notifications across the application
 */

// Initialize toast container if it doesn't exist
function initializeToastContainer() {
    if (!document.querySelector('.toast-container')) {
        const toastContainer = document.createElement('div');
        toastContainer.className = 'toast-container position-fixed top-0 end-0 p-3';
        toastContainer.style.zIndex = '1055'; // Above modals
        document.body.appendChild(toastContainer);
    }
}

/**
 * Show a toast notification
 * @param {string} title - The title of the notification
 * @param {string} message - The message content
 * @param {string} type - The type of notification ('success', 'error', 'warning', 'info')
 * @param {number} duration - Auto-hide duration in milliseconds (0 = no auto-hide)
 */
function showToast(title, message, type = 'info', duration = 5000) {
    // Ensure toast container exists
    initializeToastContainer();
    
    const toastContainer = document.querySelector('.toast-container');
    
    // Determine toast styling based on type
    const typeConfig = {
        success: {
            bgClass: 'bg-success',
            icon: 'bi bi-check-circle',
            textClass: 'text-white'
        },
        error: {
            bgClass: 'bg-danger',
            icon: 'bi bi-exclamation-circle',
            textClass: 'text-white'
        },
        warning: {
            bgClass: 'bg-warning',
            icon: 'bi bi-exclamation-triangle',
            textClass: 'text-dark'
        },
        info: {
            bgClass: 'bg-info',
            icon: 'bi bi-info-circle',
            textClass: 'text-white'
        }
    };
    
    const config = typeConfig[type] || typeConfig.info;
    const closeButtonClass = config.textClass === 'text-white' ? 'btn-close-white' : '';
    
    // Create toast element
    const toastId = 'toast-' + Date.now() + '-' + Math.random().toString(36).substr(2, 9);
    const toastHtml = `
        <div id="${toastId}" class="toast align-items-center ${config.bgClass} ${config.textClass} border-0" role="alert" aria-live="assertive" aria-atomic="true">
            <div class="d-flex">
                <div class="toast-body d-flex align-items-center">
                    <i class="${config.icon} me-2"></i>
                    <div>
                        <strong>${title}:</strong> ${message}
                    </div>
                </div>
                <button type="button" class="btn-close ${closeButtonClass} me-2 m-auto" data-bs-dismiss="toast" aria-label="Close"></button>
            </div>
        </div>
    `;
    
    // Add toast to container
    const tempDiv = document.createElement('div');
    tempDiv.innerHTML = toastHtml;
    const toastElement = tempDiv.firstElementChild;
    toastContainer.appendChild(toastElement);
    
    // Initialize Bootstrap toast
    const bsToast = new bootstrap.Toast(toastElement, {
        autohide: duration > 0,
        delay: duration
    });
    
    // Show the toast
    bsToast.show();
    
    // Clean up after toast is hidden
    toastElement.addEventListener('hidden.bs.toast', function() {
        toastElement.remove();
    });
    
    return toastElement;
}

/**
 * Convenience object for different notification types
 */
const Toast = {
    success: (title, message, duration = 5000) => showToast(title, message, 'success', duration),
    error: (title, message, duration = 7000) => showToast(title, message, 'error', duration),
    warning: (title, message, duration = 6000) => showToast(title, message, 'warning', duration),
    info: (title, message, duration = 5000) => showToast(title, message, 'info', duration)
};

// Make functions globally available
window.showToast = showToast;
window.Toast = Toast;

// Auto-initialize on DOM ready
document.addEventListener('DOMContentLoaded', function() {
    initializeToastContainer();
});

console.log('Squirrel Wiki v1.0 loaded');
