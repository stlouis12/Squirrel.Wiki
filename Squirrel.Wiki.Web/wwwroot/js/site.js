// Squirrel Wiki v3.0 - Site JavaScript

// Toast Notification System
function showToast(type, title, message) {
    const toastElement = document.getElementById('notificationToast');
    const toastIcon = document.getElementById('toastIcon');
    const toastTitle = document.getElementById('toastTitle');
    const toastBody = document.getElementById('toastBody');
    const toastHeader = toastElement.querySelector('.toast-header');
    
    // Remove any existing type classes
    toastHeader.classList.remove('bg-success', 'bg-danger', 'bg-warning', 'bg-info', 'text-white');
    toastIcon.className = 'bi me-2';
    
    // Set icon and styling based on type
    switch(type) {
        case 'success':
            toastHeader.classList.add('bg-success', 'text-white');
            toastIcon.classList.add('bi-check-circle-fill');
            break;
        case 'error':
            toastHeader.classList.add('bg-danger', 'text-white');
            toastIcon.classList.add('bi-exclamation-triangle-fill');
            break;
        case 'warning':
            toastHeader.classList.add('bg-warning');
            toastIcon.classList.add('bi-exclamation-circle-fill');
            break;
        case 'info':
            toastHeader.classList.add('bg-info', 'text-white');
            toastIcon.classList.add('bi-info-circle-fill');
            break;
        default:
            toastIcon.classList.add('bi-bell-fill');
    }
    
    // Set content
    toastTitle.textContent = title;
    toastBody.textContent = message;
    
    // Show the toast
    const toast = new bootstrap.Toast(toastElement, {
        autohide: true,
        delay: 5000
    });
    toast.show();
}

console.log('Squirrel Wiki v3.0 loaded');
